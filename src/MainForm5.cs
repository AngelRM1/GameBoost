using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Tab: Mi PC ---
        private Label lblPcVerdict = null!, lblPcDetail = null!, lblPcEstimate = null!;
        private Label lblCpu = null!, lblRam = null!, lblGpu = null!, lblDisk = null!, lblOs = null!;
        // Barras ESTATICAS (sin animacion): un panel oscuro de fondo y un
        // panel de color encima con el ancho segun la puntuacion. La barra
        // ProgressBar de WinForms tiene un brillo animado que parecia que
        // nunca terminaba de cargar; estas no se mueven nunca.
        private Panel fillCpu = null!, fillRam = null!, fillGpu = null!, fillDisk = null!, fillOverall = null!;
        private Panel trackOverall = null!;
        private Label lblOverall = null!;
        private TextBox txtBottlenecks = null!;
        private PcAnalysis? lastAnalysis;

        private Panel ScoreBar(Control parent, int x, int y, int w, int h)
        {
            var track = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Theme.CardBorder };
            var fill = new Panel { Location = new Point(0, 0), Size = new Size(0, h), BackColor = Accent };
            track.Controls.Add(fill);
            parent.Controls.Add(track);
            return fill;
        }

        private void SetScoreBar(Panel fill, int score)
        {
            if (fill.Parent == null) return;
            int s = Math.Clamp(score, 0, 100);
            fill.Width = (int)(fill.Parent.Width * s / 100.0);
            fill.BackColor = s >= 70 ? Accent : s >= 55 ? Warn : Danger;
        }

        private void BuildTabMiPc()
        {
            var p = tabMiPc;
            p.Controls.Add(Lbl("Mi PC: ¿que tan potente es tu equipo para juegos modernos?", 16, 12, 920, true));
            p.Controls.Add(Hint("GameBoost lee tu procesador, RAM, grafica y disco, y te dice donde esta el cuello de botella y en que graficos puedes jugar los juegos realistas actuales. Es una estimacion, no un benchmark.", 16, 34, 920));

            var btnAnalyze = Btn("🔍  Analizar mi PC", 16, 60, 240, 44, Accent);
            Tip(btnAnalyze, "Lee tu hardware con WMI y calcula la puntuacion. Tarda unos segundos la primera vez.");
            btnAnalyze.Click += (_, _) =>
            {
                btnAnalyze.Enabled = false;
                SetStatus("Analizando tu PC, un momento...");
                // WMI puede tardar: hacerlo en segundo plano para no congelar la ventana
                System.Threading.Tasks.Task.Run(() => PcSpecs.Analyze()).ContinueWith(t =>
                {
                    if (IsDisposed) return;
                    BeginInvoke(new Action(() =>
                    {
                        btnAnalyze.Enabled = true;
                        if (t.IsFaulted || t.Result == null) { SetStatus("No se pudo leer el hardware (WMI no respondio)."); Log("Analisis de PC fallido: WMI no respondio."); return; }
                        lastAnalysis = t.Result;
                        ShowPcAnalysis(t.Result);
                        SetStatus("Analisis listo. Puntuacion general: " + t.Result.OverallScore + "/100.");
                        Log($"Mi PC analizado: {t.Result.OverallScore}/100 - {t.Result.VerdictTitle}");
                    }));
                });
            };
            p.Controls.Add(btnAnalyze);
            var btnCopy = Btn("📋  Copiar informe", 268, 60, 200, 44, AccentBlue);
            Tip(btnCopy, "Copia todo el informe (specs, puntuacion y cuellos de botella) al portapapeles para pegarlo donde quieras.");
            btnCopy.Click += (_, _) =>
            {
                if (lastAnalysis == null) { SetStatus("Primero pulsa Analizar mi PC."); return; }
                try { Clipboard.SetText(PcSpecs.BuildReport(lastAnalysis)); SetStatus("Informe copiado al portapapeles."); } catch { SetStatus("No se pudo copiar al portapapeles."); }
            };
            var btnSave = Btn("💾  Guardar informe .txt", 480, 60, 220, 44, AccentBlue);
            Tip(btnSave, "Guarda el informe completo en un archivo de texto en tu Escritorio.");
            btnSave.Click += (_, _) =>
            {
                if (lastAnalysis == null) { SetStatus("Primero pulsa Analizar mi PC."); return; }
                try
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "GameBoost-Mi-PC.txt");
                    System.IO.File.WriteAllText(path, PcSpecs.BuildReport(lastAnalysis));
                    SetStatus("Informe guardado en el Escritorio: GameBoost-Mi-PC.txt");
                }
                catch (Exception ex) { SetStatus("No se pudo guardar: " + ex.Message); }
            };
            p.Controls.AddRange(new Control[] { btnCopy, btnSave });

            // Veredicto general
            var cardV = Card(16, 116, 940, 110);
            lblOverall = Lbl("Pulsa Analizar mi PC para ver tu puntuacion.", 12, 10, 900, true);
            trackOverall = new Panel { Location = new Point(12, 38), Size = new Size(700, 22), BackColor = Theme.CardBorder };
            fillOverall = new Panel { Location = new Point(0, 0), Size = new Size(0, 22), BackColor = Accent };
            trackOverall.Controls.Add(fillOverall);
            cardV.Controls.Add(trackOverall);
            cardV.Controls.Add(lblOverall);
            lblPcVerdict = Lbl("", 12, 66, 900, true, Accent);
            cardV.Controls.Add(lblPcVerdict);
            p.Controls.Add(cardV);
            lblPcDetail = Lbl("", 16, 232, 940, false, TextMuted);
            p.Controls.Add(lblPcDetail);
            lblPcEstimate = Lbl("", 16, 254, 940, false, TextMuted);
            p.Controls.Add(lblPcEstimate);

            // Componentes con barra
            var cardC = Card(16, 282, 580, 210);
            cardC.Controls.Add(Lbl("Tu hardware:", 12, 10, 540, true));
            lblCpu = Lbl("CPU: —", 12, 38, 380); cardC.Controls.Add(lblCpu);
            fillCpu = ScoreBar(cardC, 400, 38, 160, 20);
            lblRam = Lbl("RAM: —", 12, 68, 380); cardC.Controls.Add(lblRam);
            fillRam = ScoreBar(cardC, 400, 68, 160, 20);
            lblGpu = Lbl("Grafica: —", 12, 98, 380); cardC.Controls.Add(lblGpu);
            fillGpu = ScoreBar(cardC, 400, 98, 160, 20);
            lblDisk = Lbl("Disco: —", 12, 128, 380); cardC.Controls.Add(lblDisk);
            fillDisk = ScoreBar(cardC, 400, 128, 160, 20);
            lblOs = Lbl("Sistema: —", 12, 158, 540, false, TextMuted); cardC.Controls.Add(lblOs);
            p.Controls.Add(cardC);

            var cardB = Card(612, 282, 344, 210);
            cardB.Controls.Add(Lbl("Como leer la puntuacion:", 12, 10, 320, true));
            var help = new Label
            {
                Text = "85-100  Potente: Ultra/Alto\n70-84    Bueno: Alto a 1080p\n55-69    Medio: Medio a 1080p\n40-54    Justo: Bajo a 1080p\n0-39      Basico: mejorar primero\n\nLa grafica pesa 40%, el procesador 30%, la RAM 20% y el disco 10%.",
                Location = new Point(12, 38), Size = new Size(320, 160),
                ForeColor = TextMuted, Font = new Font("Segoe UI", 9f),
            };
            cardB.Controls.Add(help);
            p.Controls.Add(cardB);

            // Cuellos de botella
            p.Controls.Add(Lbl("Puntos criticos / cuellos de botella:", 16, 504, 920, true));
            txtBottlenecks = new TextBox
            {
                Location = new Point(16, 528), Size = new Size(940, 130),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Bg, ForeColor = TextLight, Font = new Font("Segoe UI", 9.5f),
                Text = "Analiza tu PC y aqui te dire que componente te esta frenando, por que, y que mejorar primero.",
            };
            p.Controls.Add(txtBottlenecks);
        }

        private void ShowPcAnalysis(PcAnalysis a)
        {
            var i = a.Info;
            lblOverall.Text = $"Puntuacion general: {a.OverallScore}/100";
            SetScoreBar(fillOverall, a.OverallScore);
            lblPcVerdict.Text = a.VerdictTitle;
            lblPcVerdict.ForeColor = a.OverallScore >= 70 ? Accent : a.OverallScore >= 55 ? Warn : Danger;
            lblPcDetail.Text = a.VerdictDetail;
            lblPcEstimate.Text = a.SettingsEstimate;
            lblCpu.Text = $"CPU: {i.CpuName}  ({i.CpuCores} nucleos / {i.CpuThreads} hilos, hasta {i.CpuMaxGHz} GHz)  —  {a.CpuScore}/100";
            SetScoreBar(fillCpu, a.CpuScore);
            lblRam.Text = $"RAM: {i.RamTotalGB} GB {i.RamType} {(i.RamSpeedMHz > 0 ? i.RamSpeedMHz + " MHz" : "")}  —  {a.RamScore}/100";
            SetScoreBar(fillRam, a.RamScore);
            lblGpu.Text = $"Grafica: {i.GpuName}  ({(i.GpuVramKnown ? i.GpuVramGB + " GB VRAM" : "VRAM: " + i.GpuVramSource)})  —  {a.GpuScore}/100";
            SetScoreBar(fillGpu, a.GpuScore);
            lblDisk.Text = "Disco: " + (i.Drives.Count == 0 ? "No detectado" : string.Join("  |  ", i.Drives.Select(d => $"{d.Name} {d.TotalGB} GB {d.Kind}" + (d.FreeGB > 0 ? $" ({d.FreeGB} GB libres)" : "")))) + $"  —  {a.StorageScore}/100";
            SetScoreBar(fillDisk, a.StorageScore);
            lblOs.Text = $"Sistema: {i.OsName} {i.OsBuild}  •  Placa: {i.Motherboard}  •  Pantalla: {i.Display}{(i.RefreshHz > 0 ? " a " + i.RefreshHz + " Hz" : "")}";
            txtBottlenecks.Text = string.Join(Environment.NewLine + Environment.NewLine,
                a.Bottlenecks.Select(b => $"[{b.Severity}] {b.Title}{Environment.NewLine}{b.Detail}{Environment.NewLine}Mejora: {b.Fix}"));
        }
    }
}
