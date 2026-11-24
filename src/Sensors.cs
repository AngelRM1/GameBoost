using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace GameBoost
{
    // Lectura de temperaturas, voltajes, potencia y ventiladores con
    // LibreHardwareMonitor (MIT). Necesita permisos de administrador
    // (GameBoost ya los pide) y que la placa/grafica exponga los
    // sensores: en algunos portatiles o placas baratas faltan varios.
    public enum SensorKind { Temperature, Voltage, Power, Fan, Load, Other }
    public enum HardwareKind { Cpu, Gpu, Motherboard, Memory, Storage, Other }

    public class SensorReading
    {
        public string Hardware = "";
        public HardwareKind HardwareKind;
        public string Name = "";
        public SensorKind Kind;
        public float Value;
        public string Unit = "";
    }

    public class HardwareSensors : IDisposable
    {
        private Computer? computer;
        public string? LastError { get; private set; }
        public bool IsOpen => computer != null;

        public bool TryOpen()
        {
            if (computer != null) return true;
            try
            {
                computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsMemoryEnabled = true,
                    IsStorageEnabled = true,
                    IsBatteryEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsPsuEnabled = false,
                };
                computer.Open();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                computer = null;
                return false;
            }
        }

        public List<SensorReading> Read()
        {
            var list = new List<SensorReading>();
            if (computer == null) return list;
            try
            {
                foreach (var hw in computer.Hardware) ReadHardware(hw, list);
            }
            catch { /* un sensor que falla no debe tumbar la lectura */ }
            return list;
        }

        private void ReadHardware(IHardware hw, List<SensorReading> list)
        {
            try
            {
                hw.Update();
                foreach (var sub in hw.SubHardware) ReadHardware(sub, list);
                foreach (var s in hw.Sensors)
                {
                    if (!s.Value.HasValue) continue;
                    var kind = MapKind(s.SensorType);
                    if (kind == SensorKind.Other) continue; // solo temp, voltaje, potencia, ventilador y carga
                    // Lecturas imposibles: algunos sensores ausentes reportan
                    // 255 °C (valor centinela) o valores absurdos. Una CPU/GPU
                    // real se apaga sobre los 100-115 °C, así que por encima
                    // de 125 °C (o bajo -40 °C) el sensor no existe: se ignora
                    // para no lanzar falsas alertas críticas.
                    if (kind == SensorKind.Temperature && (s.Value.Value > 125 || s.Value.Value < -40)) continue;
                    list.Add(new SensorReading
                    {
                        Hardware = hw.Name,
                        HardwareKind = MapHardware(hw.HardwareType),
                        Name = s.Name,
                        Kind = kind,
                        Value = s.Value.Value,
                        Unit = UnitFor(kind),
                    });
                }
            }
            catch { }
        }

        private static SensorKind MapKind(SensorType t) => t switch
        {
            SensorType.Temperature => SensorKind.Temperature,
            SensorType.Voltage => SensorKind.Voltage,
            SensorType.Power => SensorKind.Power,
            SensorType.Fan => SensorKind.Fan,
            SensorType.Load => SensorKind.Load,
            _ => SensorKind.Other,
        };

        private static HardwareKind MapHardware(HardwareType t) => t switch
        {
            HardwareType.Cpu => HardwareKind.Cpu,
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => HardwareKind.Gpu,
            HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController => HardwareKind.Motherboard,
            HardwareType.Memory => HardwareKind.Memory,
            HardwareType.Storage => HardwareKind.Storage,
            _ => HardwareKind.Other,
        };

        private static string UnitFor(SensorKind k) => k switch
        {
            SensorKind.Temperature => "°C",
            SensorKind.Voltage => "V",
            SensorKind.Power => "W",
            SensorKind.Fan => "RPM",
            SensorKind.Load => "%",
            _ => "",
        };

        public static string FormatValue(SensorReading r) => r.Kind switch
        {
            SensorKind.Temperature => $"{r.Value:F1} °C",
            SensorKind.Voltage => $"{r.Value:F2} V",
            SensorKind.Power => $"{r.Value:F0} W",
            SensorKind.Fan => $"{r.Value:F0} RPM",
            SensorKind.Load => $"{r.Value:F0} %",
            _ => $"{r.Value:F1} {r.Unit}",
        };

        public void Dispose()
        {
            try { computer?.Close(); } catch { }
            computer = null;
        }
    }

    // ---------- Alertas y riesgo del cable de la GPU ----------
    public enum AlertLevel { Ok, Warning, Critical }

    public class Alert
    {
        public AlertLevel Level;
        public string Title = "";
        public string Detail = "";
    }

    public class CableRisk
    {
        public AlertLevel Level;
        public float? GpuPowerW;
        public float? GpuHotspotC;
        public float? Volt12;
        public int? TdpW;
        public List<string> Reasons = new();
    }

    public class AlertResult
    {
        public AlertLevel Worst = AlertLevel.Ok;
        public List<Alert> Alerts = new();
        public CableRisk Cable = new();
    }

    public static class SensorAlerts
    {
        // Umbrales en °C / voltios / % del TDP.
        public const float CpuTempWarn = 90, CpuTempCrit = 100;
        public const float GpuTempWarn = 85, GpuTempCrit = 95;
        public const float HotspotWarn = 100, HotspotCrit = 108;
        public const float V12LowWarn = 11.4f, V12HighWarn = 12.6f, V12LowCrit = 11.0f, V12HighCrit = 13.0f;
        public const float VrmTempWarn = 85, VrmTempCrit = 95;

        // TDP de referencia (W) por modelo, para medir la potencia real
        // contra lo que la tarjeta tiene diseñado consumir. Se compara
        // por subcadena; las variantes "Ti" van antes que el modelo base.
        private static readonly (string Name, int Tdp)[] GpuTdpTable =
        {
            ("RTX 5090", 575), ("RTX 5080", 360), ("RTX 5070 Ti", 300), ("RTX 5070", 250),
            ("RTX 5060 Ti", 180), ("RTX 5060", 145), ("RTX 5050", 130),
            ("RTX 4090", 450), ("RTX 4080", 320), ("RTX 4070 Ti", 285), ("RTX 4070", 200),
            ("RTX 4060 Ti", 160), ("RTX 4060", 115),
            ("RX 9070 XT", 304), ("RX 9070", 220), ("RX 9060 XT", 182),
            ("RX 7900 XTX", 355), ("RX 7900 XT", 315), ("RX 7800 XT", 263),
            ("Arc B580", 190), ("Arc B570", 150),
        };

        public static int? TdpForGpu(string gpuName)
        {
            if (string.IsNullOrWhiteSpace(gpuName)) return null;
            foreach (var (name, tdp) in GpuTdpTable)
                if (gpuName.Contains(name, StringComparison.OrdinalIgnoreCase)) return tdp;
            return null;
        }

        public static AlertResult Evaluate(List<SensorReading> readings, string gpuName)
        {
            var result = new AlertResult();
            void Add(AlertLevel level, string title, string detail)
            {
                if (level == AlertLevel.Ok) return;
                result.Alerts.Add(new Alert { Level = level, Title = title, Detail = detail });
                if (level > result.Worst) result.Worst = level;
            }

            float? MaxTemp(Func<SensorReading, bool> pred)
            {
                // Defensa extra: aunque la lectura ya filtra, aquí también se
                // descartan temperaturas imposibles (>125 °C, p. ej. 255 °C).
                var vals = readings.Where(r => r.Kind == SensorKind.Temperature && pred(r) && r.Value <= 125 && r.Value >= -40).Select(r => r.Value);
                return vals.Any() ? vals.Max() : null;
            }

            // CPU
            var cpuMax = MaxTemp(r => r.HardwareKind == HardwareKind.Cpu);
            if (cpuMax.HasValue)
            {
                if (cpuMax >= CpuTempCrit) Add(AlertLevel.Critical, "CPU a " + cpuMax.Value.ToString("F0") + " °C", "La CPU está al límite térmico. Revisa el disipador, la pasta térmica y el flujo de aire de la caja.");
                else if (cpuMax >= CpuTempWarn) Add(AlertLevel.Warning, "CPU caliente: " + cpuMax.Value.ToString("F0") + " °C", "Por encima de 90 °C el procesador baja su velocidad para protegerse. Limpia el polvo y revisa los ventiladores.");
            }

            // GPU (núcleo). El punto caliente ("Hot Spot") se evalúa aparte
            // con sus propios umbrales, así que aquí se excluye.
            var gpuMax = MaxTemp(r => r.HardwareKind == HardwareKind.Gpu
                && (r.Name.Contains("Core") || r.Name.Contains("GPU"))
                && !r.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)
                && !r.Name.Contains("Hotspot", StringComparison.OrdinalIgnoreCase));
            if (gpuMax.HasValue)
            {
                if (gpuMax >= GpuTempCrit) Add(AlertLevel.Critical, "GPU a " + gpuMax.Value.ToString("F0") + " °C", "La gráfica está al límite. Cierra el juego y revisa sus ventiladores y el flujo de aire.");
                else if (gpuMax >= GpuTempWarn) Add(AlertLevel.Warning, "GPU caliente: " + gpuMax.Value.ToString("F0") + " °C", "Por encima de 85 °C la gráfica recorta frecuencias. Limpia el polvo y mejora la ventilación.");
            }

            // VRM / fases de alimentación de la placa
            var vrmMax = MaxTemp(r => r.Name.Contains("VRM", StringComparison.OrdinalIgnoreCase) || r.Name.Contains("MOS", StringComparison.OrdinalIgnoreCase));
            if (vrmMax.HasValue)
            {
                if (vrmMax >= VrmTempCrit) Add(AlertLevel.Critical, "VRM a " + vrmMax.Value.ToString("F0") + " °C", "Las fases de alimentación de la placa están muy calientes: riesgo de apagados y de daño a largo plazo.");
                else if (vrmMax >= VrmTempWarn) Add(AlertLevel.Warning, "VRM caliente: " + vrmMax.Value.ToString("F0") + " °C", "Las fases de alimentación sufren con carga sostenida. Mejora el aire que les llega.");
            }

            // --- Riesgo del cable de alimentación de la GPU ---
            var cable = new CableRisk();
            var gpuPowers = readings.Where(r => r.HardwareKind == HardwareKind.Gpu && r.Kind == SensorKind.Power).Select(r => r.Value);
            cable.GpuPowerW = gpuPowers.Any() ? gpuPowers.Max() : null;
            var hotspot = MaxTemp(r => r.HardwareKind == HardwareKind.Gpu && r.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
                          ?? MaxTemp(r => r.HardwareKind == HardwareKind.Gpu && r.Name.Contains("Hotspot", StringComparison.OrdinalIgnoreCase));
            cable.GpuHotspotC = hotspot;
            var v12 = readings.Where(r => r.Kind == SensorKind.Voltage && (r.Name.Contains("+12V") || r.Name == "12V" || r.Name.Contains("12V"))).Select(r => r.Value);
            cable.Volt12 = v12.Any() ? v12.First() : null;
            cable.TdpW = TdpForGpu(gpuName);

            var cableLevel = AlertLevel.Ok;
            if (cable.GpuPowerW.HasValue)
            {
                if (cable.TdpW.HasValue)
                {
                    float pct = cable.GpuPowerW.Value / cable.TdpW.Value * 100;
                    if (pct >= 110) { cableLevel = AlertLevel.Critical; cable.Reasons.Add($"La GPU consume {cable.GpuPowerW:F0} W, un {pct:F0}% de su TDP de {cable.TdpW} W: mucho más de lo diseñado."); }
                    else if (pct >= 90) { cableLevel = (AlertLevel)Math.Max((int)cableLevel, (int)AlertLevel.Warning); cable.Reasons.Add($"La GPU consume {cable.GpuPowerW:F0} W, un {pct:F0}% de su TDP de {cable.TdpW} W: cerca del límite."); }
                }
                else
                {
                    if (cable.GpuPowerW >= 550) { cableLevel = AlertLevel.Critical; cable.Reasons.Add($"La GPU consume {cable.GpuPowerW:F0} W: una potencia muy alta para un conector de alimentación."); }
                    else if (cable.GpuPowerW >= 450) { cableLevel = (AlertLevel)Math.Max((int)cableLevel, (int)AlertLevel.Warning); cable.Reasons.Add($"La GPU consume {cable.GpuPowerW:F0} W: potencia alta, el conector trabaja forzado."); }
                }
            }
            if (cable.GpuHotspotC.HasValue)
            {
                if (cable.GpuHotspotC >= HotspotCrit) { cableLevel = AlertLevel.Critical; cable.Reasons.Add($"Punto caliente de la GPU a {cable.GpuHotspotC:F0} °C: el calor se concentra junto al conector."); }
                else if (cable.GpuHotspotC >= HotspotWarn) { cableLevel = (AlertLevel)Math.Max((int)cableLevel, (int)AlertLevel.Warning); cable.Reasons.Add($"Punto caliente de la GPU a {cable.GpuHotspotC:F0} °C: calor alto cerca del conector."); }
            }
            if (cable.Volt12.HasValue)
            {
                // 12 V ±5% (11,4–12,6 V) está dentro de la especificación ATX:
                // los límites son inclusivos, el aviso empieza fuera de ellos.
                if (cable.Volt12 < V12LowCrit || cable.Volt12 > V12HighCrit) { cableLevel = AlertLevel.Critical; cable.Reasons.Add($"El carril de 12 V está en {cable.Volt12:F2} V: fuera de rango, la fuente puede estar sufriendo."); }
                else if (cable.Volt12 < V12LowWarn || cable.Volt12 > V12HighWarn) { cableLevel = (AlertLevel)Math.Max((int)cableLevel, (int)AlertLevel.Warning); cable.Reasons.Add($"El carril de 12 V está en {cable.Volt12:F2} V: se desvía de los 12 V, vigila la fuente."); }
            }
            cable.Level = cableLevel;
            result.Cable = cable;
            if (cableLevel == AlertLevel.Critical)
                Add(AlertLevel.Critical, "Riesgo en el cable de alimentación de la GPU", "Potencia, temperatura o voltaje fuera de lo seguro. " + string.Join(" ", cable.Reasons));
            else if (cableLevel == AlertLevel.Warning)
                Add(AlertLevel.Warning, "Cable de la GPU trabajando forzado", string.Join(" ", cable.Reasons));

            return result;
        }
    }
}
