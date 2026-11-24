using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Tab: Temperaturas ---
        private Panel tabTemperaturas = null!;
        private HardwareSensors hwSensors = new HardwareSensors();
        private System.Windows.Forms.Timer sensorTimer = null!;
        private DataGridView gridSensors = null!;
        private Dictionary<string, int> sensorRowIndex = new();
        private Label lblCableStatus = null!, lblCablePower = null!, lblCableHotspot = null!, lblCableVolt = null!, lblCableReasons = null!;
        private Panel bannerPanel = null!;
        private Label lblBanner = null!;
        private Label lblSensorStatus = null!;
        private CheckBox chkAutoSensors = null!;
        private bool sensorsOpened, sensorsFailed, sensorsOpening;
        private string cachedGpuName = "";
        private string lastAlertSignature = "";

        private void BuildTabTemperaturas()
        {
            var p = tabTemperaturas;
            p.AutoScrollMinSize = new Size(960, 820);
            p.Controls.Add(Lbl("Temperaturas y voltajes de tus componentes, en vivo: que nada se caliente de mas.", 16, 12, 920, true));
            p.Controls.Add(Hint("Se leen con LibreHardwareMonitor, directamente de los sensores de tu placa y tu gráfica. Si un sensor no aparece, es que tu placa no lo expone.", 16, 34, 920));

            // --- Tarjeta: riesgo del cable de la GPU ---
            var c1 = Card(16, 60, 940, 200);
            c1.Controls.Add(Lbl("Cable de alimentación de la GPU", 14, 12, 500, true));
            lblCableStatus = new Label { Text = "Leyendo sensores...", Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = TextMuted, Location = new Point(14, 38), Size = new Size(500, 28) };
            c1.Controls.Add(lblCableStatus);
            lblCablePower = Lbl("Potencia GPU: —", 14, 72, 280);
            lblCableHotspot = Lbl("Punto caliente: —", 310, 72, 280);
            lblCableVolt = Lbl("Carril 12 V: —", 606, 72, 320);
            c1.Controls.AddRange(new Control[] { lblCablePower, lblCableHotspot, lblCableVolt });
            lblCableReasons = new Label { Text = "", Location = new Point(14, 100), Size = new Size(912, 40), ForeColor = TextLight, Font = new Font("Segoe UI", 9.5f) };
            c1.Controls.Add(lblCableReasons);
            var advice = new Label
            {
                Text = "Consejo: el conector 12VHPWR debe quedar bien asentado hasta el clic, sin adaptadores en cadena ni el cable doblado junto al conector. Si huele a quemado o el conector se ve deformado, apaga la PC y no la vuelvas a encender hasta revisarlo. El software no puede medir la corriente por cada pin del conector: la alerta se basa en la potencia, la temperatura y el voltaje, que sí se pueden leer.",
                Location = new Point(14, 142), Size = new Size(912, 52), ForeColor = TextMuted, Font = new Font("Segoe UI", 9f)
            };
            c1.Controls.Add(advice);
            p.Controls.Add(c1);

            // --- Banner de alerta ---
            bannerPanel = new Panel { Location = new Point(16, 272), Size = new Size(940, 48), BackColor = PanelBg };
            lblBanner = new Label { Location = new Point(12, 6), Size = new Size(916, 36), ForeColor = TextLight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            bannerPanel.Controls.Add(lblBanner);
            p.Controls.Add(bannerPanel);

            // --- Controles ---
            var btnRefresh = Btn("Leer sensores ahora", 16, 332, 180, 38, AccentBlue);
            Tip(btnRefresh, "Lee los sensores ahora mismo, sin esperar al siguiente ciclo.");
            btnRefresh.Click += (_, _) => RefreshSensors();
            chkAutoSensors = Chk("Actualizar solo cada 2 s", 208, 338, 220, true);
            Tip(chkAutoSensors, "Si lo desmarcas, los valores solo cambian cuando pulsas Actualizar ahora.");
            lblSensorStatus = Lbl("Sensores sin leer todavía.", 440, 340, 516, false, TextMuted);
            p.Controls.AddRange(new Control[] { btnRefresh, chkAutoSensors, lblSensorStatus });

            // --- Tabla de sensores ---
            gridSensors = new DataGridView
            {
                Location = new Point(16, 380), Size = new Size(940, 330),
                BackgroundColor = CardBg, ForeColor = TextLight, BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            };
            gridSensors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHw", HeaderText = "Componente", FillWeight = 30 });
            gridSensors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSensor", HeaderText = "Sensor", FillWeight = 40 });
            gridSensors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colValue", HeaderText = "Valor", FillWeight = 15 });
            gridSensors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colState", HeaderText = "Estado", FillWeight = 15 });
            gridSensors.ColumnHeadersDefaultCellStyle.BackColor = PanelBg;
            gridSensors.ColumnHeadersDefaultCellStyle.ForeColor = TextLight;
            gridSensors.EnableHeadersVisualStyles = false;
            gridSensors.DefaultCellStyle.BackColor = CardBg;
            gridSensors.DefaultCellStyle.ForeColor = TextLight;
            gridSensors.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 58, 95);
            p.Controls.Add(gridSensors);

            p.Controls.Add(Hint("Las alertas solo se registran cuando cambian: si un valor se mantiene en aviso, no se repite el mensaje cada 2 segundos.", 16, 722, 920));
            p.Controls.Add(Hint("Nota: en máquinas virtuales y en algunas placas no hay sensores que leer. En ese caso esta pestaña lo dirá y no pasará nada.", 16, 744, 920));

            sensorTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            sensorTimer.Tick += (_, _) =>
            {
                if (IsPageSelected(tabTemperaturas) && chkAutoSensors.Checked && sensorsOpened) RefreshSensors();
            };
            sensorTimer.Start();
            // Al entrar a Temperaturas se abren los sensores: lo llama
            // SelectTab (MainForm.cs), ya no un evento del TabControl.
        }

        private void EnsureSensorsOpen()
        {
            if (sensorsOpened || sensorsFailed || sensorsOpening) return;
            sensorsOpening = true;
            lblSensorStatus.Text = "Leyendo sensores, un momento...";
            Task.Run(() =>
            {
                try { cachedGpuName = PcSpecs.GetInfo().GpuName; } catch { cachedGpuName = ""; }
                bool ok = hwSensors.TryOpen();
                if (!IsDisposed) BeginInvoke(new Action(() =>
                {
                    sensorsOpening = false;
                    if (ok) { sensorsOpened = true; RefreshSensors(); }
                    else
                    {
                        sensorsFailed = true;
                        lblSensorStatus.Text = "No se pudieron leer los sensores en este equipo.";
                        lblCableStatus.Text = "Sensores no disponibles";
                        lblCableStatus.ForeColor = Warn;
                        lblCableReasons.Text = "LibreHardwareMonitor no pudo abrir los sensores" + (string.IsNullOrEmpty(hwSensors.LastError) ? "." : ": " + hwSensors.LastError) + " Algunas placas y portátiles no exponen estos datos, y en máquinas virtuales no existen. El resto de GameBoost sigue funcionando igual.";
                        Log("Temperaturas: no se pudieron leer los sensores. " + (hwSensors.LastError ?? ""));
                    }
                }));
            });
        }

        private void RefreshSensors()
        {
            if (!sensorsOpened) { EnsureSensorsOpen(); return; }
            List<SensorReading> readings;
            try { readings = hwSensors.Read(); }
            catch { return; }
            if (readings.Count == 0)
            {
                lblSensorStatus.Text = "No se encontró ningún sensor con valor en este equipo.";
                return;
            }
            var result = SensorAlerts.Evaluate(readings, cachedGpuName);
            UpdateCableCard(result.Cable);
            UpdateSensorGrid(readings);
            UpdateBanner(result);
            lblSensorStatus.Text = $"{readings.Count} sensores  •  Última lectura: {DateTime.Now:HH:mm:ss}  •  GPU: {(string.IsNullOrEmpty(cachedGpuName) ? "no detectada" : cachedGpuName)}";
        }

        private void UpdateCableCard(CableRisk cable)
        {
            lblCablePower.Text = "Potencia GPU: " + (cable.GpuPowerW.HasValue ? $"{cable.GpuPowerW:F0} W" + (cable.TdpW.HasValue ? $"  (TDP {cable.TdpW} W)" : "") : "—");
            lblCableHotspot.Text = "Punto caliente: " + (cable.GpuHotspotC.HasValue ? $"{cable.GpuHotspotC:F0} °C" : "—");
            lblCableVolt.Text = "Carril 12 V: " + (cable.Volt12.HasValue ? $"{cable.Volt12:F2} V" : "—");
            switch (cable.Level)
            {
                case AlertLevel.Critical:
                    lblCableStatus.Text = "CRÍTICO: riesgo en el cable de la GPU";
                    lblCableStatus.ForeColor = Danger;
                    break;
                case AlertLevel.Warning:
                    lblCableStatus.Text = "Aviso: el cable de la GPU trabaja forzado";
                    lblCableStatus.ForeColor = Warn;
                    break;
                default:
                    lblCableStatus.Text = "Bien: cable de la GPU dentro de lo normal";
                    lblCableStatus.ForeColor = Accent;
                    break;
            }
            lblCableReasons.Text = cable.Reasons.Count > 0 ? string.Join(" ", cable.Reasons) : "Potencia, punto caliente y carril de 12 V dentro de los valores seguros.";
        }

        private void UpdateSensorGrid(List<SensorReading> readings)
        {
            var seen = new HashSet<string>();
            foreach (var r in readings.OrderBy(r => r.Hardware).ThenBy(r => r.Name))
            {
                string key = r.Hardware + " | " + r.Name;
                seen.Add(key);
                string state = SensorState(r);
                if (!sensorRowIndex.TryGetValue(key, out int idx))
                {
                    idx = gridSensors.Rows.Add(r.Hardware, r.Name, HardwareSensors.FormatValue(r), state);
                    sensorRowIndex[key] = idx;
                }
                else
                {
                    var row = gridSensors.Rows[idx];
                    row.Cells["colValue"].Value = HardwareSensors.FormatValue(r);
                    row.Cells["colState"].Value = state;
                }
                var cell = gridSensors.Rows[sensorRowIndex[key]].Cells["colState"];
                cell.Style.ForeColor = state == "CRÍTICO" ? Danger : state == "AVISO" ? Warn : Accent;
            }
            // Quita filas de sensores que ya no aparecen (de mayor a menor
            // índice, para que borrar no mueva las filas que faltan)
            var gone = sensorRowIndex.Where(kv => !seen.Contains(kv.Key)).OrderByDescending(kv => kv.Value).ToList();
            foreach (var kv in gone)
            {
                if (kv.Value < gridSensors.Rows.Count) gridSensors.Rows.RemoveAt(kv.Value);
                sensorRowIndex.Remove(kv.Key);
            }
            if (gone.Count > 0)
                sensorRowIndex = gridSensors.Rows.Cast<DataGridViewRow>()
                    .ToDictionary(row => (string)row.Cells["colHw"].Value + " | " + (string)row.Cells["colSensor"].Value, row => row.Index);
        }

        private static string SensorState(SensorReading r)
        {
            if (r.Kind == SensorKind.Temperature)
            {
                float warn, crit;
                if (r.HardwareKind == HardwareKind.Cpu) { warn = SensorAlerts.CpuTempWarn; crit = SensorAlerts.CpuTempCrit; }
                else if (r.HardwareKind == HardwareKind.Gpu) { warn = SensorAlerts.GpuTempWarn; crit = SensorAlerts.GpuTempCrit; }
                else if (r.Name.Contains("VRM", StringComparison.OrdinalIgnoreCase) || r.Name.Contains("MOS", StringComparison.OrdinalIgnoreCase)) { warn = SensorAlerts.VrmTempWarn; crit = SensorAlerts.VrmTempCrit; }
                else { warn = 85; crit = 95; }
                if (r.Value >= crit) return "CRÍTICO";
                if (r.Value >= warn) return "AVISO";
                return "Bien";
            }
            if (r.Kind == SensorKind.Voltage && (r.Name.Contains("12V") || r.Name.Contains("+12V")))
            {
                if (r.Value < SensorAlerts.V12LowCrit || r.Value > SensorAlerts.V12HighCrit) return "CRÍTICO";
                if (r.Value < SensorAlerts.V12LowWarn || r.Value > SensorAlerts.V12HighWarn) return "AVISO";
                return "Bien";
            }
            return "—";
        }

        private void UpdateBanner(AlertResult result)
        {
            string signature = string.Join(";", result.Alerts.Select(a => ((int)a.Level) + ":" + a.Title));
            if (signature != lastAlertSignature)
            {
                lastAlertSignature = signature;
                if (result.Worst > AlertLevel.Ok)
                {
                    foreach (var a in result.Alerts)
                        Log($"ALERTA {(a.Level == AlertLevel.Critical ? "CRÍTICA" : "de aviso")}: {a.Title} — {a.Detail}");
                }
            }
            if (result.Worst == AlertLevel.Critical)
            {
                bannerPanel.BackColor = Color.FromArgb(69, 26, 26);
                lblBanner.ForeColor = Color.FromArgb(254, 202, 202);
                lblBanner.Text = "⚠ " + result.Alerts.First(a => a.Level == AlertLevel.Critical).Title + " — " + result.Alerts.First(a => a.Level == AlertLevel.Critical).Detail;
            }
            else if (result.Worst == AlertLevel.Warning)
            {
                bannerPanel.BackColor = Color.FromArgb(69, 44, 8);
                lblBanner.ForeColor = Color.FromArgb(253, 230, 138);
                var a = result.Alerts.First(x => x.Level == AlertLevel.Warning);
                lblBanner.Text = "⚠ " + a.Title + " — " + a.Detail;
            }
            else
            {
                bannerPanel.BackColor = PanelBg;
                lblBanner.ForeColor = Accent;
                lblBanner.Text = "✓ Sin alertas: temperaturas, voltajes y potencia dentro de lo normal.";
            }
        }
    }
}
