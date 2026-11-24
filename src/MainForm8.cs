using System;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Seccion Rendimiento de Windows (paquete recomendado v1.6.0) ---
        private CheckBox chkGameMode = null!, chkHags = null!, chkDvrOff = null!;
        private Label lblGameState = null!, lblPlanState = null!, lblDvrState = null!;
        private Label lblHiberState = null!, lblUpdCache = null!, lblFrag = null!, lblDiskWarn = null!;
        private ListBox lstDisks = null!;
        private Button btnHiberOff = null!, btnHiberOn = null!, btnOptimizeDisk = null!;

        private void BuildPerfSection()
        {
            var p = tabOptimizar;
            p.Controls.Add(Lbl("Rendimiento de Windows (para juegos): ajustes del sistema que se notan al jugar. Todo se puede deshacer.", 16, 700, 920, true));
            p.Controls.Add(Hint("Si algo se siente raro, usa 'Deshacer todo lo de esta seccion' abajo: vuelve todo a como estaba.", 16, 722, 920));

            // --- Modo Juego + HAGS ---
            var c5 = Card(16, 748, 460, 300);
            c5.Controls.Add(Lbl("Modo Juego de Windows + HAGS", 12, 10, 420, true));
            lblGameState = Lbl("Estado actual: ...", 12, 34, 430); c5.Controls.Add(lblGameState);
            chkGameMode = Chk("Modo Juego de Windows (prioriza el juego)", 12, 62, 430, true); c5.Controls.Add(chkGameMode);
            chkHags = Chk("HAGS: la GPU gestiona su propia memoria", 12, 88, 430, true); c5.Controls.Add(chkHags);
            var btnPerfApply = Btn("Aplicar Modo Juego + HAGS", 12, 122, 260, 38, Accent);
            Tip(btnPerfApply, "Activa lo marcado arriba. El Modo Juego se nota al momento; HAGS, al reiniciar.");
            btnPerfApply.Click += (_, _) =>
            {
                Log("=== Aplicando Modo Juego + HAGS ===");
                WindowsGameTweaks.SetGameMode(chkGameMode.Checked, Log);
                WindowsGameTweaks.SetHags(chkHags.Checked, Log);
                RefreshPerfState();
                SetStatus("Modo Juego + HAGS aplicados. Si activaste HAGS, reinicia la PC para que se note.");
            };
            c5.Controls.Add(btnPerfApply);
            c5.Controls.Add(Lbl("El Modo Juego se nota al momento. HAGS solo se activa al reiniciar la PC y solo funciona si tu grafica y su driver lo soportan; si no, Windows lo ignora sin romper nada.", 12, 172, 430, false, TextMuted, 54));
            c5.Controls.Add(Lbl("Para deshacerlo usa 'Deshacer todo lo de esta seccion' abajo.", 12, 232, 430, false, TextMuted, 40));
            p.Controls.Add(c5);

            // --- Energia y grabacion ---
            var c6 = Card(492, 748, 464, 300);
            c6.Controls.Add(Lbl("Energia y grabacion", 12, 10, 420, true));
            lblPlanState = Lbl("Plan actual: ...", 12, 34, 430); c6.Controls.Add(lblPlanState);
            var btnUltimate = Btn("Poner Maximo rendimiento", 12, 62, 208, 38, Accent);
            Tip(btnUltimate, "El plan oculto de Windows para escritorio: la CPU no baja su velocidad para ahorrar. Gasta mas luz.");
            btnUltimate.Click += (_, _) => { Log("=== Plan Maximo rendimiento ==="); PowerPlanService.ApplyUltimate(Log); RefreshPerfState(); SetStatus("Plan Maximo rendimiento aplicado."); };
            var btnBalanced = Btn("Volver a Equilibrado", 232, 62, 200, 38, AccentBlue);
            Tip(btnBalanced, "Vuelve al plan Equilibrado de siempre, el que gasta menos luz.");
            btnBalanced.Click += (_, _) => { PowerPlanService.ApplyBalanced(Log); RefreshPerfState(); SetStatus("Plan Equilibrado aplicado."); };
            c6.Controls.AddRange(new Control[] { btnUltimate, btnBalanced });
            lblDvrState = Lbl("Game DVR: ...", 12, 116, 430); c6.Controls.Add(lblDvrState);
            chkDvrOff = Chk("Desactivar Game DVR (la grabacion de Xbox roba FPS)", 12, 142, 430, true); c6.Controls.Add(chkDvrOff);
            var btnDvrApply = Btn("Aplicar Game DVR", 12, 172, 200, 38, Accent);
            Tip(btnDvrApply, "Apaga o enciende la grabacion en segundo plano de Xbox segun lo marcado arriba.");
            btnDvrApply.Click += (_, _) => { WindowsGameTweaks.SetGameDvr(!chkDvrOff.Checked, Log); RefreshPerfState(); SetStatus("Game DVR actualizado."); };
            c6.Controls.Add(btnDvrApply);
            c6.Controls.Add(Lbl("Game DVR graba tus partidas en segundo plano aunque no pulses nada. Apagarlo libera FPS y espacio en disco.", 12, 224, 430, false, TextMuted, 54));
            p.Controls.Add(c6);

            // --- Liberar espacio ---
            var c7 = Card(16, 1060, 460, 300);
            c7.Controls.Add(Lbl("Liberar espacio (GB que no usas)", 12, 10, 420, true));
            lblHiberState = Lbl("Hibernacion: ...", 12, 34, 430); c7.Controls.Add(lblHiberState);
            btnHiberOff = Btn("Apagar hibernacion", 12, 62, 200, 38, Accent);
            Tip(btnHiberOff, "Borra el archivo hiberfil.sys y libera varios GB. Tambien se apaga el inicio rapido.");
            btnHiberOff.Click += (_, _) => { HibernationService.Set(false, Log); RefreshPerfState(); SetStatus("Hibernacion apagada."); };
            btnHiberOn = Btn("Encender hibernacion", 224, 62, 208, 38, AccentBlue);
            Tip(btnHiberOn, "Vuelve a encender la hibernacion (y el inicio rapido).");
            btnHiberOn.Click += (_, _) => { HibernationService.Set(true, Log); RefreshPerfState(); SetStatus("Hibernacion encendida."); };
            c7.Controls.AddRange(new Control[] { btnHiberOff, btnHiberOn });
            c7.Controls.Add(Lbl("Apagarla tambien apaga el inicio rapido de Windows. Se puede volver a encender cuando quieras.", 12, 112, 430, false, TextMuted, 40));
            lblUpdCache = Lbl("Cache de Windows Update: sin calcular", 12, 158, 430); c7.Controls.Add(lblUpdCache);
            var btnUpdCalc = Btn("Calcular", 12, 186, 200, 38, AccentBlue);
            Tip(btnUpdCalc, "Suma cuanto pesan las descargas viejas de Windows Update, sin borrar nada todavia.");
            btnUpdCalc.Click += (_, _) => CalcUpdateCache();
            var btnUpdClean = Btn("Limpiar cache de Update", 224, 186, 208, 38, Accent);
            Tip(btnUpdClean, "Borra las descargas viejas de actualizaciones ya instaladas. Windows vuelve a bajar lo que necesite.");
            btnUpdClean.Click += (_, _) => CleanUpdateCache();
            c7.Controls.AddRange(new Control[] { btnUpdCalc, btnUpdClean });
            c7.Controls.Add(Lbl("Son descargas de actualizaciones ya instaladas. Windows vuelve a bajar lo que necesite, no se pierde nada.", 12, 234, 430, false, TextMuted, 54));
            p.Controls.Add(c7);

            // --- Salud del disco ---
            var c8 = Card(492, 1060, 464, 300);
            c8.Controls.Add(Lbl("Salud del disco", 12, 10, 420, true));
            lstDisks = new ListBox { Location = new Point(12, 36), Size = new Size(440, 96), BackColor = Bg, ForeColor = TextLight, SelectionMode = SelectionMode.None };
            c8.Controls.Add(lstDisks);
            lblFrag = Lbl("Fragmentacion: sin analizar", 12, 140, 430); c8.Controls.Add(lblFrag);
            var btnAnalyze = Btn("Analizar discos", 12, 168, 200, 38, AccentBlue);
            Tip(btnAnalyze, "Mide que tan fragmentado esta cada disco. Es rapido y no cambia nada.");
            btnAnalyze.Click += (_, _) => AnalyzeDisks();
            btnOptimizeDisk = Btn("Optimizar (TRIM/defrag)", 224, 168, 208, 38, Accent);
            Tip(btnOptimizeDisk, "En SSD hace TRIM (rapido) y en HDD desfragmenta (puede tardar varios minutos). No se borra nada.");
            btnOptimizeDisk.Click += (_, _) => OptimizeDisks();
            c8.Controls.AddRange(new Control[] { btnAnalyze, btnOptimizeDisk });
            lblDiskWarn = Lbl("", 12, 214, 430, true, Danger); c8.Controls.Add(lblDiskWarn);
            c8.Controls.Add(Lbl("En SSD hace TRIM (rapido) y en HDD desfragmenta (puede tardar varios minutos). No se borra nada.", 12, 244, 430, false, TextMuted, 54));
            p.Controls.Add(c8);

            RefreshPerfState();
        }

        private void RefreshPerfState()
        {
            try
            {
                bool gm = WindowsGameTweaks.GetGameMode();
                int hags = WindowsGameTweaks.GetHags();
                lblGameState.Text = "Estado actual: Modo Juego " + (gm ? "activado" : "desactivado") + ", HAGS " + (hags == 2 ? "activado" : "desactivado") + ".";
                chkGameMode.Checked = gm;
                chkHags.Checked = hags == 2;

                lblPlanState.Text = "Plan actual: " + PowerPlanService.GetActiveName();

                bool dvr = WindowsGameTweaks.GetGameDvr();
                lblDvrState.Text = "Game DVR: " + (dvr ? "activado (graba en segundo plano)" : "desactivado") + ".";
                chkDvrOff.Checked = !dvr;

                bool hib = HibernationService.IsEnabled();
                var gb = HibernationService.HiberFileGb();
                lblHiberState.Text = "Hibernacion: " + (hib ? "activada" + (gb != null ? $", hiberfil.sys pesa {gb:F1} GB" : "") : "desactivada") + ".";
                btnHiberOff.Enabled = hib;
                btnHiberOn.Enabled = !hib;

                var disks = DiskHealthService.ListDisks();
                lstDisks.Items.Clear();
                foreach (var d in disks)
                    lstDisks.Items.Add($"{d.Letters}  {d.Type}  {d.SizeGb:F0} GB  —  {(d.Healthy ? "Correcto" : "Revisar: " + d.Status)}");
                if (disks.Count == 0) lstDisks.Items.Add("No se detectaron discos con letra.");
                var bad = disks.Where(d => !d.Healthy).ToList();
                lblDiskWarn.Text = bad.Count > 0
                    ? "¡Ojo! El disco " + string.Join(", ", bad.Select(d => d.Letters)) + " reporta fallos (SMART): haz copia de seguridad cuanto antes."
                    : "";
            }
            catch (Exception ex) { Log("Rendimiento: no se pudo leer el estado (" + ex.Message + ")."); }
        }

        private void CalcUpdateCache()
        {
            lblUpdCache.Text = "Calculando, un momento...";
            Task.Run(() => UpdateCacheService.Measure()).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { lblUpdCache.Text = "Cache de Windows Update: no se pudo calcular."; return; }
                    lblUpdCache.Text = "Cache de Windows Update: " + OptimizePaths.FormatBytes(t.Result) + " en descargas viejas.";
                }));
            });
        }

        private void CleanUpdateCache()
        {
            long bytes = UpdateCacheService.Measure();
            if (bytes == 0) { MessageBox.Show("La cache de Windows Update ya esta vacia.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var r = MessageBox.Show("Se van a borrar las descargas viejas de Windows Update (" + OptimizePaths.FormatBytes(bytes) + ").\n\nSon actualizaciones ya instaladas: Windows vuelve a bajar lo que necesite. ¿Limpiar ahora?",
                "GameBoost - Confirmar limpieza", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            lblUpdCache.Text = "Limpiando, un momento...";
            Task.Run(() => UpdateCacheService.Clean(Log)).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { lblUpdCache.Text = "Cache de Windows Update: no se pudo limpiar."; return; }
                    var (deleted, skipped, freed) = t.Result;
                    lblUpdCache.Text = "Cache de Windows Update: liberados " + OptimizePaths.FormatBytes(freed) + " (" + deleted + " archivos, " + skipped + " saltados).";
                    lblOptStatus.Text = "Update: " + lblUpdCache.Text;
                    SetStatus(lblUpdCache.Text);
                }));
            });
        }

        private void AnalyzeDisks()
        {
            lblFrag.Text = "Analizando, un momento...";
            Task.Run(() =>
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var d in DiskHealthService.ListDisks())
                {
                    var letter = d.Letters.Split(',')[0].Trim();
                    var (ok, pct, _) = DiskHealthService.Analyze(letter);
                    parts.Add(letter + ": " + (ok && pct != null ? pct + "% fragmentado" : "no se pudo analizar"));
                }
                return string.Join("  |  ", parts);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    lblFrag.Text = "Fragmentacion: " + (t.IsFaulted ? "no se pudo analizar." : t.Result);
                }));
            });
        }

        private void OptimizeDisks()
        {
            var disks = DiskHealthService.ListDisks();
            if (disks.Count == 0) { MessageBox.Show("No se detectaron discos con letra.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var r = MessageBox.Show("Se va a optimizar cada disco (" + string.Join(", ", disks.Select(d => d.Letters + " " + d.Type)) + ").\n\nEn SSD es TRIM (rapido) y en HDD es desfragmentar (puede tardar varios minutos). No se borra nada. ¿Optimizar ahora?",
                "GameBoost - Confirmar optimizacion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            btnOptimizeDisk.Enabled = false;
            lblFrag.Text = "Optimizando, esto puede tardar varios minutos...";
            Task.Run(() =>
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var d in disks)
                {
                    var letter = d.Letters.Split(',')[0].Trim();
                    Log("=== Optimizando disco " + letter + " (" + d.Type + ") ===");
                    var (code, _) = DiskHealthService.Optimize(letter);
                    lines.Add(letter + ": " + (code == 0 ? "optimizado" : "no se pudo optimizar (codigo " + code + ")"));
                }
                return string.Join("  |  ", lines);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    btnOptimizeDisk.Enabled = true;
                    lblFrag.Text = "Fragmentacion: " + (t.IsFaulted ? "no se pudo optimizar." : t.Result);
                    lblOptStatus.Text = "Discos: " + lblFrag.Text;
                    SetStatus("Optimizacion de discos terminada.");
                    Log("Discos optimizados: " + lblFrag.Text);
                }));
            });
        }
    }
}
