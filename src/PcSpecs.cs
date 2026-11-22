using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using Microsoft.Win32;

namespace GameBoost
{
    // Lectura de hardware por WMI (Win32_*) con fallback seguro:
    // si una consulta falla, ese dato queda como "No detectado" en vez de romper el analisis.
    public class DriveInfo2
    {
        public string Name = "";
        public double TotalGB;
        public double FreeGB;
        public string Kind = ""; // NVMe / SSD / HDD / ?
    }

    public class PcInfo
    {
        public string CpuName = "No detectado";
        public int CpuCores;
        public int CpuThreads;
        public double CpuMaxGHz;
        public double RamTotalGB;
        public int RamSpeedMHz;
        public string RamType = "No detectado";
        public string GpuName = "No detectado";
        public double GpuVramGB;
        // GpuVramKnown=false significa: la VRAM real no se pudo leer con
        // fiabilidad (WMI la capa a 4 GB). En ese caso GpuVramGB NO es un
        // dato real y nunca debe usarse para decir "poca VRAM".
        public bool GpuVramKnown;
        public string GpuVramSource = "";
        public string GpuDriver = "";
        public List<DriveInfo2> Drives = new();
        public string OsName = "No detectado";
        public string OsBuild = "";
        public string Motherboard = "No detectado";
        public string Display = "No detectado";
        public int RefreshHz;
    }

    public class Bottleneck
    {
        public string Severity = ""; // CRITICO / AVISO / BIEN
        public string Title = "";
        public string Detail = "";
        public string Fix = "";
    }

    public class PcAnalysis
    {
        public PcInfo Info = new();
        public int CpuScore, RamScore, GpuScore, StorageScore, OverallScore;
        public string VerdictTitle = "";
        public string VerdictDetail = "";
        public string SettingsEstimate = "";
        public List<Bottleneck> Bottlenecks = new();
    }

    public static class PcSpecs
    {
        private static object? Get(ManagementObject o, string name)
        {
            try { return o[name]; } catch { return null; }
        }
        private static string Str(object? v, string def = "") => v == null ? def : Convert.ToString(v, CultureInfo.InvariantCulture) ?? def;
        private static double Num(object? v) { try { return v == null ? 0 : Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0; } }

        private static IEnumerable<ManagementObject> Query(string wql)
        {
            var list = new List<ManagementObject>();
            try
            {
                using var s = new ManagementObjectSearcher(wql);
                foreach (ManagementObject o in s.Get()) list.Add(o);
            }
            catch { /* WMI no disponible: se queda vacio y se marca No detectado */ }
            return list;
        }

        public static PcInfo GetInfo()
        {
            var info = new PcInfo();
            try
            {
                foreach (var o in Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                {
                    info.CpuName = Str(Get(o, "Name")).Trim();
                    info.CpuCores = (int)Num(Get(o, "NumberOfCores"));
                    info.CpuThreads = (int)Num(Get(o, "NumberOfLogicalProcessors"));
                    info.CpuMaxGHz = Math.Round(Num(Get(o, "MaxClockSpeed")) / 1000.0, 2);
                    break;
                }
                double ramBytes = 0; int speed = 0; string type = "";
                foreach (var o in Query("SELECT Capacity, Speed, MemoryType FROM Win32_PhysicalMemory"))
                {
                    ramBytes += Num(Get(o, "Capacity"));
                    int s = (int)Num(Get(o, "Speed")); if (s > speed) speed = s;
                    int mt = (int)Num(Get(o, "MemoryType"));
                    type = mt switch { 20 => "DDR", 21 => "DDR2", 22 => "DDR2 FB-DIMM", 24 => "DDR3", 25 => "DDR4", 26 => "DDR4", 34 => "DDR5", 27 => "DDR4", _ => type };
                    // Win32 usa SMBIOS: 26=DDR4, 34=DDR5 en equipos nuevos
                    if (mt == 26) type = "DDR4";
                    if (mt == 34) type = "DDR5";
                }
                info.RamTotalGB = Math.Round(ramBytes / 1024.0 / 1024 / 1024, 1);
                info.RamSpeedMHz = speed;
                if (!string.IsNullOrEmpty(type)) info.RamType = type;

                double bestVram = 0; string bestGpu = "";
                foreach (var o in Query("SELECT Name, AdapterRAM, DriverVersion, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController"))
                {
                    string name = Str(Get(o, "Name")).Trim();
                    double vram = Num(Get(o, "AdapterRAM")) / 1024.0 / 1024 / 1024;
                    if (vram > bestVram || string.IsNullOrEmpty(bestGpu)) { bestVram = vram; bestGpu = name; info.GpuDriver = Str(Get(o, "DriverVersion")); }
                    int w = (int)Num(Get(o, "CurrentHorizontalResolution"));
                    int h = (int)Num(Get(o, "CurrentVerticalResolution"));
                    int hz = (int)Num(Get(o, "CurrentRefreshRate"));
                    if (w > 0 && h > 0) { info.Display = $"{w} x {h}"; info.RefreshHz = hz; }
                }
                info.GpuName = string.IsNullOrEmpty(bestGpu) ? "No detectado" : bestGpu;
                // VRAM real: WMI AdapterRAM es UInt32 y se capa en 4 GB
                // (los drivers devuelven 0xFFFFFFFF = 4.294.967.295 bytes).
                // Una RTX 5080 de 16 GB aparecia como "4 GB" por eso.
                var (vramGB, known, source) = ResolveVram(info.GpuName, bestVram);
                info.GpuVramGB = Math.Round(vramGB, 1);
                info.GpuVramKnown = known;
                info.GpuVramSource = source;

                var driveKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in Query("SELECT Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive"))
                {
                    string model = Str(Get(o, "Model")).Trim();
                    string media = Str(Get(o, "MediaType")).Trim();
                    string iface = Str(Get(o, "InterfaceType")).Trim();
                    double size = Num(Get(o, "Size")) / 1024.0 / 1024 / 1024;
                    string kind = "SSD";
                    if (media.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase) && iface.Contains("IDE", StringComparison.OrdinalIgnoreCase)) kind = "HDD";
                    if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) || iface.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) kind = "NVMe";
                    else if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase)) kind = "SSD";
                    else if (model.Contains("HDD", StringComparison.OrdinalIgnoreCase) || model.Contains("Hard Disk", StringComparison.OrdinalIgnoreCase)) kind = "HDD";
                    driveKind[model] = kind;
                    info.Drives.Add(new DriveInfo2 { Name = model, TotalGB = Math.Round(size, 0), Kind = kind });
                }
                foreach (var o in Query("SELECT DeviceID, Size, FreeSpace, DriveType FROM Win32_LogicalDisk"))
                {
                    if ((int)Num(Get(o, "DriveType")) != 3) continue;
                    string id = Str(Get(o, "DeviceID"));
                    double total = Num(Get(o, "Size")) / 1024.0 / 1024 / 1024;
                    double free = Num(Get(o, "FreeSpace")) / 1024.0 / 1024 / 1024;
                    // Empareja la unidad logica con el disco fisico mas parecido en tamano, si hay
                    var d = info.Drives.OrderBy(x => Math.Abs(x.TotalGB - total)).FirstOrDefault();
                    if (d != null && Math.Abs(d.TotalGB - total) < Math.Max(50, total * 0.1))
                    { d.FreeGB = Math.Round(free, 0); if (!string.IsNullOrEmpty(id)) d.Name = d.Name + "  (" + id + ")"; }
                    else info.Drives.Add(new DriveInfo2 { Name = id, TotalGB = Math.Round(total, 0), FreeGB = Math.Round(free, 0), Kind = "?" });
                }

                foreach (var o in Query("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem"))
                {
                    info.OsName = Str(Get(o, "Caption")).Trim() + " " + Str(Get(o, "OSArchitecture")).Trim();
                    info.OsBuild = "Version " + Str(Get(o, "Version")) + " (compilacion " + Str(Get(o, "BuildNumber")) + ")";
                    break;
                }
                foreach (var o in Query("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    info.Motherboard = Str(Get(o, "Manufacturer")).Trim() + " " + Str(Get(o, "Product")).Trim();
                    break;
                }
            }
            catch { /* nunca romper la app por un dato */ }
            return info;
        }

        // VRAM real, en orden de fiabilidad. Nunca devuelve el tope de
        // WMI (4 GB) como si fuera el valor real.
        // Tabla de GPUs actualizada a noviembre de 2026 (ver ScoreGpu).
        public static (double Gb, bool Known, string Source) ResolveVram(string gpuName, double wmiVramGB)
        {
            // WMI solo es fiable si NO esta en el tope: el tope es
            // 4.294.967.295 bytes = 3.999... GB. Un valor de 8, 12 o 16 GB
            // es real y se acepta; solo ~4.0 GB es sospechoso de ser el tope.
            bool capped = wmiVramGB >= 3.99 && wmiVramGB <= 4.01;
            if (wmiVramGB > 0 && !capped) return (wmiVramGB, true, "WMI");

            // 1) nvidia-smi: el driver NVIDIA da la VRAM exacta en MiB.
            if (gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                || gpuName.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
                || gpuName.Contains("RTX", StringComparison.OrdinalIgnoreCase)
                || gpuName.Contains("GTX", StringComparison.OrdinalIgnoreCase)
                || gpuName.Contains("Quadro", StringComparison.OrdinalIgnoreCase))
            {
                double gb = VramFromNvidiaSmi();
                if (gb > 0) return (gb, true, "nvidia-smi");
            }

            // 2) Registro: HardwareInformation.qwMemorySize es un QWORD
            // (64 bits), sin el limite de 4 GB de WMI.
            double regGb = VramFromRegistry();
            if (regGb > 0) return (regGb, true, "registro");

            // 3) Sin fuente fiable: sabemos que es MAS de 4 GB (porque WMI
            // llego al tope), pero no el valor exacto. Mejor decirlo asi
            // que inventar un "4 GB".
            if (capped) return (0, false, "mas de 4 GB (WMI no lo puede leer)");
            return (0, false, "no detectada");
        }

        private static double VramFromNvidiaSmi()
        {
            try
            {
                var (code, output) = ProcessRunner.Run("nvidia-smi", "--query-gpu=memory.total --format=csv,noheader,nounits");
                if (code != 0) return 0;
                double best = 0;
                foreach (var line in output.Split('\n'))
                {
                    if (double.TryParse(line.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double mib) && mib > best)
                        best = mib;
                }
                return best > 0 ? best / 1024.0 : 0;
            }
            catch { return 0; }
        }

        private static double VramFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (key == null) return 0;
                long best = 0;
                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var s = key.OpenSubKey(sub);
                        var v = s?.GetValue("HardwareInformation.qwMemorySize");
                        if (v is long l && l > best) best = l;
                        if (v is int ii && ii > best) best = ii;
                    }
                    catch { }
                }
                return best > 0 ? best / 1024.0 / 1024 / 1024 : 0;
            }
            catch { return 0; }
        }

        // --- Puntuacion (0-100 por componente) ---
        public static int ScoreCpu(PcInfo i)
        {
            if (i.CpuCores <= 0) return 0;
            int s = i.CpuCores >= 8 ? 82 : i.CpuCores >= 6 ? 74 : i.CpuCores == 4 ? 55 : 30;
            if (i.CpuMaxGHz >= 4.0) s += 8; else if (i.CpuMaxGHz >= 3.5) s += 5; else if (i.CpuMaxGHz >= 3.0) s += 2;
            string n = i.CpuName;
            if (n.Contains("i9", StringComparison.OrdinalIgnoreCase) || n.Contains("Ryzen 9", StringComparison.OrdinalIgnoreCase)) s += 8;
            else if (n.Contains("i7", StringComparison.OrdinalIgnoreCase) || n.Contains("Ryzen 7", StringComparison.OrdinalIgnoreCase)) s += 5;
            else if (n.Contains("i5", StringComparison.OrdinalIgnoreCase) || n.Contains("Ryzen 5", StringComparison.OrdinalIgnoreCase)) s += 2;
            return Math.Clamp(s, 0, 100);
        }

        public static int ScoreRam(PcInfo i)
        {
            if (i.RamTotalGB <= 0) return 0;
            int s = i.RamTotalGB >= 32 ? 95 : i.RamTotalGB >= 16 ? 85 : i.RamTotalGB >= 12 ? 65 : i.RamTotalGB >= 8 ? 45 : 25;
            if (i.RamSpeedMHz > 0 && i.RamSpeedMHz < 2400) s -= 8;
            if (i.RamType == "DDR5") s += 3;
            return Math.Clamp(s, 0, 100);
        }

        public static int ScoreGpu(PcInfo i)
        {
            string n = i.GpuName.ToLowerInvariant();
            int tier = -1;
            // NVIDIA (de mas a menos potente). Tabla actualizada nov 2026:
            // serie RTX 50 (ene 2025). Revisar las variantes "Ti" antes que
            // la base, porque "rtx 5070 ti" tambien contiene "rtx 5070".
            if (n.Contains("rtx 5090")) tier = 99;
            else if (n.Contains("rtx 4090")) tier = 98;
            else if (n.Contains("rtx 5080")) tier = 94;
            else if (n.Contains("rtx 4080")) tier = 92;
            else if (n.Contains("rtx 5070 ti")) tier = 90;
            else if (n.Contains("rtx 4070 ti")) tier = 88;
            else if (n.Contains("rtx 5070")) tier = 86;
            else if (n.Contains("rtx 4070")) tier = 85;
            else if (n.Contains("rtx 5060 ti")) tier = 81;
            else if (n.Contains("rtx 4060 ti")) tier = 80;
            else if (n.Contains("rtx 3080")) tier = 88;
            else if (n.Contains("rtx 3070")) tier = 82;
            else if (n.Contains("rtx 5060")) tier = 78;
            else if (n.Contains("rtx 4060")) tier = 77;
            else if (n.Contains("rtx 3060 ti")) tier = 76;
            else if (n.Contains("rtx 3060")) tier = 73;
            else if (n.Contains("rtx 2080")) tier = 72;
            else if (n.Contains("rtx 2070")) tier = 68;
            else if (n.Contains("rtx 2060")) tier = 62;
            else if (n.Contains("rtx 3050")) tier = 60;
            else if (n.Contains("rtx 5050")) tier = 58;
            else if (n.Contains("gtx 1660")) tier = 55;
            else if (n.Contains("gtx 1080") || n.Contains("gtx 1070")) tier = 55;
            else if (n.Contains("gtx 1060")) tier = 48;
            else if (n.Contains("gtx 1650")) tier = 45;
            // AMD (RDNA 4: RX 9070/9060, mar-jun 2025)
            else if (n.Contains("rx 7900 xtx")) tier = 95;
            else if (n.Contains("rx 9070 xt")) tier = 89;
            else if (n.Contains("rx 7900")) tier = 90;
            else if (n.Contains("rx 7800")) tier = 86;
            else if (n.Contains("rx 9070")) tier = 84;
            else if (n.Contains("rx 6800")) tier = 83;
            else if (n.Contains("rx 7700")) tier = 81;
            else if (n.Contains("rx 9060 xt")) tier = 77;
            else if (n.Contains("rx 6700")) tier = 76;
            else if (n.Contains("rx 7600")) tier = 72;
            else if (n.Contains("rx 9060")) tier = 68;
            else if (n.Contains("rx 6600")) tier = 68;
            else if (n.Contains("rx 5700")) tier = 65;
            else if (n.Contains("rx 5600")) tier = 55;
            else if (n.Contains("rx 580") || n.Contains("rx 590")) tier = 48;
            // Intel Arc (Battlemage B580/B570, dic 2024)
            else if (n.Contains("arc b580")) tier = 73;
            else if (n.Contains("arc a770")) tier = 76;
            else if (n.Contains("arc a750")) tier = 71;
            else if (n.Contains("arc b570")) tier = 67;
            else if (n.Contains("arc a580")) tier = 63;
            else if (n.Contains("arc a380")) tier = 45;
            // Integradas
            else if (n.Contains("uhd") || n.Contains("iris xe") || n.Contains("radeon graphics") && !n.Contains("rx")) tier = 28;
            if (tier >= 0) return tier;
            // Sin modelo conocido: estimar solo por VRAM, y solo si es fiable.
            // Si la VRAM tampoco se pudo leer, no castigar con 25: 50 neutro.
            if (!i.GpuVramKnown) return 50;
            return i.GpuVramGB >= 12 ? 80 : i.GpuVramGB >= 8 ? 70 : i.GpuVramGB >= 6 ? 55 : i.GpuVramGB >= 4 ? 40 : 25;
        }

        public static int ScoreStorage(PcInfo i)
        {
            if (i.Drives.Count == 0) return 0;
            if (i.Drives.Any(d => d.Kind == "NVMe")) return 95;
            if (i.Drives.Any(d => d.Kind == "SSD")) return 80;
            if (i.Drives.All(d => d.Kind == "HDD")) return 30;
            return 60;
        }

        public static PcAnalysis Analyze() => Analyze(GetInfo());

        public static PcAnalysis Analyze(PcInfo info)
        {
            var a = new PcAnalysis { Info = info };
            a.CpuScore = ScoreCpu(info);
            a.RamScore = ScoreRam(info);
            a.GpuScore = ScoreGpu(info);
            a.StorageScore = ScoreStorage(info);
            a.OverallScore = (int)Math.Round(a.GpuScore * 0.40 + a.CpuScore * 0.30 + a.RamScore * 0.20 + a.StorageScore * 0.10);

            // Veredicto general
            if (a.OverallScore >= 85)
            { a.VerdictTitle = "🟢 Potente para juegos modernos"; a.VerdictDetail = "Tu equipo esta por encima de lo que piden los juegos realistas actuales. Puedes jugar en Alto o Ultra a 1080p y 1440p con buena fluidez."; a.SettingsEstimate = "Estimado: 1080p Ultra, 1440p Alto, 4K Medio/Alto segun el juego."; }
            else if (a.OverallScore >= 70)
            { a.VerdictTitle = "🟢 Bueno para juegos modernos"; a.VerdictDetail = "Tu equipo corre bien los juegos realistas actuales. En los mas exigentes (como GTA VI en PC o Battlefield 6) puede que tengas que bajar algun ajuste."; a.SettingsEstimate = "Estimado: 1080p Alto, 1440p Medio/Alto."; }
            else if (a.OverallScore >= 55)
            { a.VerdictTitle = "🟡 Medio: juega, pero con ajustes"; a.VerdictDetail = "Tu equipo corre los juegos modernos, pero los mas realistas pediran graficos en Medio y alguna bajada de resolucion o de texturas."; a.SettingsEstimate = "Estimado: 1080p Medio/Alto, 1440p Bajo/Medio."; }
            else if (a.OverallScore >= 40)
            { a.VerdictTitle = "🟠 Justo para juegos modernos"; a.VerdictDetail = "Los juegos realistas actuales te van a costar: graficos en Bajo, posibles tirones y cargas lentas. Hay un cuello de botella claro abajo."; a.SettingsEstimate = "Estimado: 1080p Bajo/Medio. Los juegos muy exigentes pueden no ir fluidos."; }
            else
            { a.VerdictTitle = "🔴 Basico para juegos modernos"; a.VerdictDetail = "Tu equipo esta por debajo de lo que piden los juegos realistas actuales. Conviene mejorar el componente marcado como critico antes de comprar un juego exigente."; a.SettingsEstimate = "Estimado: 1080p Bajo. Revisa los puntos criticos antes de jugar."; }

            // Cuellos de botella, de mas a menos grave
            if (info.RamTotalGB > 0 && info.RamTotalGB < 16)
                a.Bottlenecks.Add(new Bottleneck { Severity = "CRITICO", Title = "RAM insuficiente: " + info.RamTotalGB + " GB", Detail = "Los juegos modernos piden 16 GB como minimo. Con menos, Windows usa el disco como memoria y aparecen tirones y cargas lentas.", Fix = "Amplia a 16 GB (ideal 32 GB). Es la mejora mas barata que mas se nota." });
            if (info.GpuVramKnown && info.GpuVramGB > 0 && info.GpuVramGB < 8)
                a.Bottlenecks.Add(new Bottleneck { Severity = "CRITICO", Title = "Poca VRAM en la grafica: " + info.GpuVramGB + " GB", Detail = "Las texturas realistas no caben en " + info.GpuVramGB + " GB, asi que el juego baja la calidad solo o da tirones al cargar texturas.", Fix = "Baja Texturas a Medio/Bajo. Para jugar en Alto, busca una grafica con 8 GB o mas." });
            if (a.GpuScore > 0 && a.GpuScore < 55)
                a.Bottlenecks.Add(new Bottleneck { Severity = "CRITICO", Title = "Grafica limitada para juegos realistas", Detail = "Tu grafica (" + info.GpuName + ") esta por debajo de lo que piden los juegos actuales en graficos altos.", Fix = "Juega en Bajo/Medio a 1080p. Si quieres subir a Alto, la grafica es lo primero a cambiar." });
            if (info.Drives.Count > 0 && info.Drives.All(d => d.Kind == "HDD"))
                a.Bottlenecks.Add(new Bottleneck { Severity = "CRITICO", Title = "Disco duro (HDD), no SSD", Detail = "Los juegos modernos cargan muchisimo mas rapido en SSD. En HDD las cargas son lentas y pueden aparecer tirones al entrar a zonas nuevas.", Fix = "Instala los juegos en un SSD (mejor NVMe). Un SSD SATA ya es un salto enorme." });
            if (info.CpuCores > 0 && info.CpuCores < 6)
                a.Bottlenecks.Add(new Bottleneck { Severity = "AVISO", Title = "Procesador con pocos nucleos: " + info.CpuCores + " nucleos", Detail = "Los juegos actuales aprovechan 6-8 nucleos. Con menos, el procesador se satura y el juego puede ir a tirones aunque la grafica sea buena.", Fix = "Cierra apps en segundo plano con el Modo Juego. Al cambiar de PC, busca 6 nucleos o mas." });
            if (info.RamSpeedMHz > 0 && info.RamSpeedMHz < 2400)
                a.Bottlenecks.Add(new Bottleneck { Severity = "AVISO", Title = "RAM lenta: " + info.RamSpeedMHz + " MHz", Detail = "La velocidad de la RAM afecta a lo rapido que el procesador recibe datos. Por debajo de 2400 MHz se nota en juegos.", Fix = "Si tu placa lo permite, activa el perfil XMP/EXPO en la BIOS o cambia a RAM mas rapida." });
            var sysDrive = info.Drives.FirstOrDefault();
            if (sysDrive != null && sysDrive.TotalGB > 0 && sysDrive.FreeGB > 0 && sysDrive.FreeGB < 20)
                a.Bottlenecks.Add(new Bottleneck { Severity = "AVISO", Title = "Poco espacio libre: " + sysDrive.FreeGB + " GB libres", Detail = "Windows y los juegos necesitan espacio libre para actualizaciones y archivos temporales. Con menos de 20 GB, todo va mas lento.", Fix = "Libera espacio o mueve juegos a otro disco. GTA VI, por ejemplo, pide hasta 212 GB." });
            if (a.Bottlenecks.Count == 0)
                a.Bottlenecks.Add(new Bottleneck { Severity = "BIEN", Title = "Sin cuellos de botella criticos", Detail = "Ningun componente esta claramente por debajo de lo que piden los juegos modernos. Si un juego va lento, revisa primero el ping y las apps en segundo plano.", Fix = "Usa el Modo Juego y el Medidor para comprobar que no sea la red." });

            return a;
        }

        public static string BuildReport(PcAnalysis a)
        {
            var i = a.Info;
            var lines = new List<string>
            {
                "GameBoost - Informe de mi PC - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                "",
                "Puntuacion general: " + a.OverallScore + "/100 - " + a.VerdictTitle,
                a.VerdictDetail,
                a.SettingsEstimate,
                "",
                "CPU: " + i.CpuName + " (" + i.CpuCores + " nucleos / " + i.CpuThreads + " hilos, hasta " + i.CpuMaxGHz + " GHz) - " + a.CpuScore + "/100",
                "RAM: " + i.RamTotalGB + " GB " + i.RamType + " " + (i.RamSpeedMHz > 0 ? i.RamSpeedMHz + " MHz" : "") + " - " + a.RamScore + "/100",
                "Grafica: " + i.GpuName + " (" + (i.GpuVramKnown ? i.GpuVramGB + " GB VRAM" : "VRAM: " + i.GpuVramSource) + ", driver " + i.GpuDriver + ") - " + a.GpuScore + "/100",
                "Almacenamiento: " + string.Join("; ", i.Drives.Select(d => d.Name + " " + d.TotalGB + " GB " + d.Kind + (d.FreeGB > 0 ? ", " + d.FreeGB + " GB libres" : ""))) + " - " + a.StorageScore + "/100",
                "Sistema: " + i.OsName + " " + i.OsBuild,
                "Placa base: " + i.Motherboard,
                "Pantalla: " + i.Display + (i.RefreshHz > 0 ? " a " + i.RefreshHz + " Hz" : ""),
                "",
                "Cuellos de botella:",
            };
            foreach (var b in a.Bottlenecks) lines.Add("- [" + b.Severity + "] " + b.Title + " | " + b.Detail + " | Mejora: " + b.Fix);
            lines.Add("");
            lines.Add("Nota: la puntuacion es una estimacion basada en los requisitos habituales de los juegos modernos (2026). No es un benchmark: el rendimiento real depende del juego, los drivers y la temperatura.");
            lines.Add("Tabla de GPUs actualizada a noviembre de 2026 (series RTX 50, RX 9070/9060, Arc Battlemage). La VRAM se lee con nvidia-smi o el registro cuando WMI no puede (WMI capa la VRAM en 4 GB).");
            return string.Join(Environment.NewLine, lines);
        }
    }

    // Ajustes persistentes (combinacion de emergencia) en %AppData%\GameBoost\settings.json
    public class AppSettings
    {
        public bool EmCtrl { get; set; } = true;
        public bool EmAlt { get; set; } = true;
        public bool EmShift { get; set; } = false;
        public bool EmWin { get; set; } = false;
        public string EmKey { get; set; } = "F12";

        public static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameBoost", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(Path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }
        public void Save()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
        public string Display => (EmCtrl ? "Ctrl + " : "") + (EmAlt ? "Alt + " : "") + (EmShift ? "Shift + " : "") + (EmWin ? "Win + " : "") + EmKey;
    }
}
