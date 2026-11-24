using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Tab: Optimizar PC ---
        private CheckBox clTemp = null!, clShader = null!, clRecycle = null!, clUpdate = null!;
        private Label lblCleanSize = null!;
        private CheckedListBox lstStartup = null!;
        private List<StartupItem> startupItems = new();
        private CheckedListBox lstServices = null!;
        private Label lblServiceWhy = null!;
        private CheckBox fxAnim = null!, fxTransp = null!, fxPerf = null!;
        private Label lblOptStatus = null!;

        private void BuildTabOptimizar()
        {
            var p = tabOptimizar;
            p.AutoScrollMinSize = new Size(960, 1500);
            p.Controls.Add(Lbl("Optimizar PC: limpia, arranca mas rapido y quita lo que estorba. Todo se puede deshacer.", 16, 12, 920, true));
            p.Controls.Add(Hint("Nada de esto borra tus documentos, fotos ni juegos, y todo se puede restaurar. Antes de aplicar un cambio, GameBoost guarda como estaba.", 16, 34, 920));

            // --- Limpieza ---
            var c1 = Card(16, 60, 460, 300);
            c1.Controls.Add(Lbl("Limpieza de disco", 12, 10, 420, true));
            clTemp = Chk("Archivos temporales de Windows y del usuario", 12, 38, 420, true); Tip(clTemp, "Basura que dejan los programas al instalarse o actualizarse. Borrarla no afecta a nada que uses."); c1.Controls.Add(clTemp);
            clShader = Chk("Caché de DirectX / shaders (D3DCache, NVIDIA, AMD)", 12, 64, 420, true); Tip(clShader, "Shaders compilados de tus juegos. Se borran sin problema, el juego los vuelve a crear la proxima vez (la primera carga puede tardar un poco mas)."); c1.Controls.Add(clShader);
            clRecycle = Chk("Vaciar la Papelera", 12, 90, 420, false); Tip(clRecycle, "Vacia la Papelera de reciclaje. Ojo: lo que haya ahi se pierde de verdad, revisala antes."); c1.Controls.Add(clRecycle);
            clUpdate = Chk("Limpieza de Windows Update (DISM)", 12, 116, 420, false); Tip(clUpdate, "Borra los archivos viejos de actualizaciones de Windows. Tarda varios minutos y se hace en segundo plano."); c1.Controls.Add(clUpdate);
            lblCleanSize = Lbl("Pulsa el boton de arriba para ver cuanto se libera.", 12, 148, 420, false, TextMuted); c1.Controls.Add(lblCleanSize);
            var btnCalc = Btn("Ver cuanto se puede liberar", 12, 182, 200, 38, AccentBlue);
            Tip(btnCalc, "Suma cuanto pesan los temporales y la cache, sin borrar nada todavia.");
            btnCalc.Click += (_, _) => CalcCleanSpace();
            var btnClean = Btn("Limpiar lo marcado", 224, 182, 208, 38, Accent);
            Tip(btnClean, "Borra lo marcado arriba. Te pedira confirmacion antes, y te dira cuanto libero al final.");
            btnClean.Click += (_, _) => DoClean();
            c1.Controls.AddRange(new Control[] { btnCalc, btnClean });
            c1.Controls.Add(Hint("No se borran archivos usados ahora mismo ni modificados hace menos de 1 hora.", 12, 232, 420));
            p.Controls.Add(c1);

            // --- Arranque ---
            var c2 = Card(492, 60, 464, 300);
            c2.Controls.Add(Lbl("Programas de inicio", 12, 10, 420, true));
            c2.Controls.Add(Hint("Desmarcar uno solo evita que se abra solo al encender. El programa sigue instalado.", 12, 34, 420));
            lstStartup = new CheckedListBox { Location = new Point(12, 58), Size = new Size(440, 150), BackColor = Bg, ForeColor = TextLight, CheckOnClick = true };
            c2.Controls.Add(lstStartup);
            var btnStartRefresh = Btn("Volver a leer la lista", 12, 220, 200, 38, AccentBlue);
            Tip(btnStartRefresh, "Vuelve a leer que programas se abren al encender la PC.");
            btnStartRefresh.Click += (_, _) => LoadStartupList();
            var btnStartApply = Btn("Guardar cambios de inicio", 224, 220, 208, 38, Accent);
            Tip(btnStartApply, "Aplica lo marcado: los desmarcados ya no se abriran solos al encender.");
            btnStartApply.Click += (_, _) => ApplyStartupChanges();
            c2.Controls.AddRange(new Control[] { btnStartRefresh, btnStartApply });
            c2.Controls.Add(Hint("Si no sabes que es un programa, dejalo marcado. No desactives tu antivirus.", 12, 264, 420));
            p.Controls.Add(c2);

            // --- Servicios ---
            var c3 = Card(16, 372, 460, 300);
            c3.Controls.Add(Lbl("Servicios en segundo plano", 12, 10, 420, true));
            c3.Controls.Add(Hint("Se ponen en Manual y se detienen. Si un juego los necesita, Windows los arranca solo.", 12, 34, 420));
            lstServices = new CheckedListBox { Location = new Point(12, 58), Size = new Size(440, 150), BackColor = Bg, ForeColor = TextLight, CheckOnClick = true };
            foreach (var o in WindowsServices.Options()) lstServices.Items.Add(o.Display, o.DefaultChecked);
            lstServices.SelectedIndexChanged += (_, _) =>
            {
                int i = lstServices.SelectedIndex;
                if (i >= 0 && i < WindowsServices.Options().Count) lblServiceWhy.Text = WindowsServices.Options()[i].Why;
            };
            c3.Controls.Add(lstServices);
            lblServiceWhy = Lbl("Selecciona un servicio para ver para que sirve.", 12, 212, 420, false, TextMuted); c3.Controls.Add(lblServiceWhy);
            var btnSvcApply = Btn("Aplicar a los marcados", 12, 240, 200, 38, Accent);
            Tip(btnSvcApply, "Detiene los servicios marcados y los deja en Manual. Guarda como estaban para poder restaurarlos.");
            btnSvcApply.Click += (_, _) => ApplyServices();
            var btnSvcRestore = Btn("Restaurar servicios", 224, 240, 208, 38, AccentBlue);
            Tip(btnSvcRestore, "Devuelve todos los servicios a como estaban antes.");
            btnSvcRestore.Click += (_, _) => { foreach (var o in WindowsServices.Options()) WindowsServices.Restore(o.Name, Log); SetStatus("Servicios restaurados."); };
            c3.Controls.AddRange(new Control[] { btnSvcApply, btnSvcRestore });
            p.Controls.Add(c3);

            // --- Efectos visuales ---
            var c4 = Card(492, 372, 464, 300);
            c4.Controls.Add(Lbl("Efectos visuales", 12, 10, 420, true));
            fxAnim = Chk("Quitar animaciones de ventanas (minimizar, abrir)", 12, 38, 420, true); Tip(fxAnim, "Las ventanas aparecen de golpe, sin animacion. La PC se siente mas rapida."); c4.Controls.Add(fxAnim);
            fxTransp = Chk("Quitar transparencia de Windows", 12, 64, 420, true); Tip(fxTransp, "Quita el efecto de cristal de la barra y los menus. Ahorra un poco de grafica."); c4.Controls.Add(fxTransp);
            fxPerf = Chk("Ajustar para mejor rendimiento (sombras, suavizado...)", 12, 90, 420, true); Tip(fxPerf, "Es el ajuste 'Mejor rendimiento' de Windows: quita sombras, bordes suaves y demas adornos. Se nota mas en PCs justas."); c4.Controls.Add(fxPerf);
            var btnFxApply = Btn("Aplicar efectos marcados", 12, 130, 200, 38, Accent);
            Tip(btnFxApply, "Aplica lo marcado arriba. Los cambios se ven al momento.");
            btnFxApply.Click += (_, _) => { ApplyVisualEffects(); };
            var btnFxRestore = Btn("Restaurar efectos", 224, 130, 208, 38, AccentBlue);
            Tip(btnFxRestore, "Devuelve los efectos a como estaban antes.");
            btnFxRestore.Click += (_, _) => { VisualEffectsService.Restore(Log); SetStatus("Efectos visuales restaurados."); };
            c4.Controls.AddRange(new Control[] { btnFxApply, btnFxRestore });
            c4.Controls.Add(Hint("Si te gustan las animaciones, no marques nada. Esto es solo para que la PC responda mas rapido.", 12, 182, 420));
            p.Controls.Add(c4);

            BuildPerfSection();

            var btnRestoreAll = Btn("↩  Deshacer todo lo de esta seccion", 16, 1372, 340, 44, Danger);
            Tip(btnRestoreAll, "Devuelve inicio, servicios, efectos y rendimiento a como estaban antes de tocarlos.");
            btnRestoreAll.Click += (_, _) =>
            {
                StartupService.RestoreAll(Log);
                foreach (var o in WindowsServices.Options()) WindowsServices.Restore(o.Name, Log);
                VisualEffectsService.Restore(Log);
                WindowsGameTweaks.RestoreAll(Log);
                LoadStartupList();
                RefreshPerfState();
                SetStatus("Todo restaurado: inicio, servicios, efectos y rendimiento.");
            };
            p.Controls.Add(btnRestoreAll);

            lblOptStatus = Lbl("Optimizar PC: listo. Empieza por ver cuanto se puede liberar, la lista de inicio o Rendimiento de Windows abajo.", 16, 1428, 940, true, TextMuted);
            p.Controls.Add(lblOptStatus);

            LoadStartupList();
        }

        private List<string> SelectedCleanPaths()
        {
            var paths = new List<string>();
            var locs = CleanupService.TempLocations();
            if (clTemp.Checked) { foreach (var l in locs.Where(x => x.Label.Contains("temporales"))) paths.Add(l.Path); }
            if (clShader.Checked) { foreach (var l in locs.Where(x => x.Label.Contains("Caché"))) paths.Add(l.Path); }
            return paths;
        }

        private void CalcCleanSpace()
        {
            lblCleanSize.Text = "Calculando, un momento...";
            var paths = SelectedCleanPaths();
            System.Threading.Tasks.Task.Run(() => CleanupService.Measure(paths)).ContinueWith(t =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { lblCleanSize.Text = "No se pudo calcular."; return; }
                    var (files, bytes) = t.Result;
                    lblCleanSize.Text = $"Se pueden liberar unos {OptimizePaths.FormatBytes(bytes)} en {files} archivos.";
                    lblOptStatus.Text = "Limpieza: " + lblCleanSize.Text;
                }));
            });
        }

        private void DoClean()
        {
            var paths = SelectedCleanPaths();
            var (files, bytes) = CleanupService.Measure(paths);
            string extra = (clRecycle.Checked ? "\n• Vaciar la Papelera" : "") + (clUpdate.Checked ? "\n• Limpieza de Windows Update (tarda minutos)" : "");
            if (files == 0 && !clRecycle.Checked && !clUpdate.Checked) { MessageBox.Show("No hay nada que limpiar con lo marcado.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var r = MessageBox.Show($"Se van a borrar unos {OptimizePaths.FormatBytes(bytes)} en {files} archivos temporales.{extra}\n\nTus documentos, fotos y juegos NO se tocan. ¿Limpiar ahora?",
                "GameBoost - Confirmar limpieza", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            var (deleted, skipped, freed) = CleanupService.Clean(paths, Log);
            if (clRecycle.Checked) CleanupService.EmptyRecycleBin(Log);
            if (clUpdate.Checked) CleanupService.WindowsUpdateCleanupAsync(Log, msg => { if (!IsDisposed) BeginInvoke(new Action(() => SetStatus(msg))); });
            lblCleanSize.Text = $"Liberados {OptimizePaths.FormatBytes(freed)} ({deleted} archivos, {skipped} saltados por estar en uso).";
            lblOptStatus.Text = "Limpieza: " + lblCleanSize.Text;
            SetStatus(lblCleanSize.Text);
            Log($"Limpieza: {deleted} archivos borrados, {OptimizePaths.FormatBytes(freed)} liberados, {skipped} saltados.");
        }

        private void LoadStartupList()
        {
            startupItems = StartupService.List();
            lstStartup.Items.Clear();
            foreach (var it in startupItems)
                lstStartup.Items.Add($"{it.Name}  —  {it.Location}" + (it.Enabled ? "" : "  (desactivado)"), it.Enabled);
            if (startupItems.Count == 0) lstStartup.Items.Add("(No se encontraron programas de inicio)");
        }

        private void ApplyStartupChanges()
        {
            int changed = 0;
            for (int i = 0; i < startupItems.Count && i < lstStartup.Items.Count; i++)
            {
                bool want = lstStartup.GetItemChecked(i);
                if (want != startupItems[i].Enabled)
                {
                    if (StartupService.SetEnabled(startupItems[i], want, Log)) changed++;
                }
            }
            LoadStartupList();
            lblOptStatus.Text = changed == 0 ? "Inicio: sin cambios." : $"Inicio: {changed} programa(s) actualizado(s).";
            SetStatus(lblOptStatus.Text);
        }

        private void ApplyServices()
        {
            var opts = WindowsServices.Options();
            int n = 0;
            for (int i = 0; i < opts.Count && i < lstServices.Items.Count; i++)
            {
                if (lstServices.GetItemChecked(i)) { WindowsServices.ApplyManual(opts[i].Name, Log); n++; }
            }
            lblOptStatus.Text = n == 0 ? "Servicios: no marcaste ninguno." : $"Servicios: {n} servicio(s) puesto(s) en Manual y detenido(s).";
            SetStatus(lblOptStatus.Text);
        }

        private void ApplyVisualEffects()
        {
            if (!fxAnim.Checked && !fxTransp.Checked && !fxPerf.Checked)
            {
                MessageBox.Show("No marcaste ningún efecto. Marca al menos uno.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            VisualEffectsService.ApplyPerformance(fxAnim.Checked, fxTransp.Checked, fxPerf.Checked, Log);
            lblOptStatus.Text = "Efectos visuales aplicados para mejor rendimiento.";
            SetStatus(lblOptStatus.Text);
        }
    }
}
