using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Tab: Perfiles por juego + modo automatico (v1.7.0) ---
        private Panel tabPerfiles = null!;
        private ListBox lstProfiles = null!;
        private Label lblProfileExe = null!, lblProfileDetail = null!, lblAutoStatus = null!, lblProfileStatus = null!;
        private CheckBox chkAutoMode = null!;
        private Button btnSaveProfile = null!, btnLoadProfile = null!, btnRenameProfile = null!;

        private List<GameProfile> profiles = new();
        private bool autoModeOn = false;
        private System.Windows.Forms.Timer? autoTimer;
        private bool autoActivated = false;
        private int autoGamePid = 0;
        private int autoSuppressedPid = 0;
        private string autoProfileName = "";

        private void BuildTabPerfiles()
        {
            var p = tabPerfiles;
            p.Controls.Add(Lbl("Perfiles por juego: cada juego con sus ajustes, listos en un clic.", 16, 12, 920, true));
            p.Controls.Add(Hint("Crea un perfil con los ajustes de Modo Juego de tu juego favorito. El modo automatico lo activa solo cuando se abre el juego.", 16, 34, 920));

            // --- Tus perfiles ---
            var c1 = Card(16, 60, 460, 392);
            c1.Controls.Add(Lbl("Tus perfiles", 12, 10, 420, true));
            lstProfiles = new ListBox { Location = new Point(12, 36), Size = new Size(436, 140), BackColor = Bg, ForeColor = TextLight };
            lstProfiles.SelectedIndexChanged += (_, _) => ShowSelectedProfile();
            c1.Controls.Add(lstProfiles);
            lblProfileExe = Lbl("", 12, 184, 430, true); c1.Controls.Add(lblProfileExe);
            lblProfileDetail = Lbl("", 12, 206, 430, false, TextMuted, 40); c1.Controls.Add(lblProfileDetail);
            var btnNewProfile = Btn("＋  Nuevo perfil (desde Modo Juego)", 12, 252, 436, 38, Accent);
            Tip(btnNewProfile, "Crea un perfil con el juego y los ajustes que tienes ahora en la pestana Modo Juego. Te pedira un nombre.");
            btnNewProfile.Click += (_, _) => NewProfile();
            c1.Controls.Add(btnNewProfile);
            var btnApplyProfile = Btn("Aplicar y activar", 12, 298, 208, 38, Accent);
            Tip(btnApplyProfile, "Activa el Modo Juego con los ajustes de este perfil.");
            btnApplyProfile.Click += (_, _) => ApplySelectedProfile();
            var btnDeleteProfile = Btn("Eliminar", 232, 298, 200, 38, Danger);
            Tip(btnDeleteProfile, "Borra el perfil seleccionado. No toca nada de tu PC.");
            btnDeleteProfile.Click += (_, _) => DeleteSelectedProfile();
            c1.Controls.AddRange(new Control[] { btnApplyProfile, btnDeleteProfile });
            btnLoadProfile = Btn("Cargar", 12, 344, 134, 38, AccentBlue);
            Tip(btnLoadProfile, "Pone los ajustes de este perfil en la pestana Modo Juego para que los cambies y luego pulses Guardar.");
            btnLoadProfile.Click += (_, _) => LoadSelectedProfileIntoTab();
            btnSaveProfile = Btn("Guardar", 154, 344, 134, 38, AccentBlue);
            Tip(btnSaveProfile, "Guarda los ajustes actuales de Modo Juego en el perfil seleccionado.");
            btnSaveProfile.Click += (_, _) => SaveSelectedProfileFromTab();
            btnRenameProfile = Btn("Renombrar", 296, 344, 136, 38, AccentBlue);
            Tip(btnRenameProfile, "Cambia el nombre del perfil seleccionado.");
            btnRenameProfile.Click += (_, _) => RenameSelectedProfile();
            c1.Controls.AddRange(new Control[] { btnLoadProfile, btnSaveProfile, btnRenameProfile });
            p.Controls.Add(c1);

            // --- Modo automatico ---
            var c2 = Card(492, 60, 464, 380);
            c2.Controls.Add(Lbl("Modo automatico", 12, 10, 420, true));
            chkAutoMode = Chk("Activar el Modo Juego solo cuando se abra un juego con perfil", 12, 36, 430, true);
            Tip(chkAutoMode, "Si lo marcas, GameBoost vigila en segundo plano y activa el Modo Juego solo al detectar tu juego.");
            chkAutoMode.CheckedChanged += (_, _) => SetAutoMode(chkAutoMode.Checked);
            c2.Controls.Add(chkAutoMode);
            lblAutoStatus = Lbl("Modo automatico: apagado.", 12, 66, 430, true, Accent); c2.Controls.Add(lblAutoStatus);
            c2.Controls.Add(Lbl("Cuando abras un juego que tenga perfil, GameBoost activa el Modo Juego solo con sus ajustes. Al cerrar el juego, lo desactiva y deja todo como estaba.", 12, 104, 430, false, TextMuted, 54));
            c2.Controls.Add(Lbl("Si lo apagas a mano mientras juegas, no se vuelve a activar solo hasta que cierres y vuelvas a abrir el juego.", 12, 164, 430, false, TextMuted, 54));
            c2.Controls.Add(Lbl("Solo vigila los juegos que tienen perfil. Si abres un juego sin perfil, no pasa nada.", 12, 224, 430, false, TextMuted, 54));
            c2.Controls.Add(Lbl("Para que funcione, GameBoost tiene que estar abierto (puede estar minimizado).", 12, 284, 430, false, TextMuted, 54));
            p.Controls.Add(c2);

            lblProfileStatus = Lbl("Perfiles: listos.", 16, 460, 940, true, TextMuted);
            p.Controls.Add(lblProfileStatus);

            LoadProfiles();
        }

        private GameProfile? SelectedProfile()
        {
            int i = lstProfiles.SelectedIndex;
            return i >= 0 && i < profiles.Count ? profiles[i] : null;
        }

        private void LoadProfiles()
        {
            var file = ProfileStore.Load();
            profiles = file.Profiles;
            autoModeOn = file.AutoMode;
            RefreshProfileList();
            chkAutoMode.Checked = autoModeOn;
            if (autoModeOn) StartAutoTimer();
            UpdateAutoStatus();
        }

        private void SaveProfiles()
        {
            ProfileStore.Save(new ProfilesFile { Profiles = profiles, AutoMode = autoModeOn });
        }

        private void RefreshProfileList()
        {
            int sel = lstProfiles.SelectedIndex;
            lstProfiles.Items.Clear();
            foreach (var pr in profiles) lstProfiles.Items.Add(pr.Name);
            if (profiles.Count > 0) lstProfiles.SelectedIndex = Math.Min(Math.Max(sel, 0), profiles.Count - 1);
            ShowSelectedProfile();
        }

        private void ShowSelectedProfile()
        {
            var pr = SelectedProfile();
            bool has = pr != null;
            btnLoadProfile.Enabled = has;
            btnSaveProfile.Enabled = has;
            btnRenameProfile.Enabled = has;
            if (!has)
            {
                lblProfileExe.Text = "No tienes perfiles todavia.";
                lblProfileDetail.Text = "Abre tu juego, pon los ajustes en Modo Juego y pulsa 'Nuevo perfil'.";
                return;
            }
            lblProfileExe.Text = pr!.Exe;
            lblProfileDetail.Text = pr.Summary();
        }

        private void NewProfile()
        {
            var exe = txtGameExe.Text.Trim();
            if (string.IsNullOrEmpty(exe))
            {
                PickGameProcess();
                exe = txtGameExe.Text.Trim();
                if (string.IsNullOrEmpty(exe)) return;
            }
            var existing = profiles.FirstOrDefault(x => string.Equals(x.Exe, exe, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                lstProfiles.SelectedIndex = profiles.IndexOf(existing);
                MessageBox.Show("Ya existe un perfil para este juego. Si quieres cambiar sus ajustes, cargalo en Modo Juego, cambia lo que quieras y pulsa Guardar.",
                    "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var name = Interaction.InputBox("Nombre del perfil:", "Nuevo perfil", Path.GetFileNameWithoutExtension(exe));
            if (string.IsNullOrWhiteSpace(name)) return;
            var pr = CurrentTabProfile(name.Trim(), exe);
            profiles.Add(pr);
            SaveProfiles();
            RefreshProfileList();
            lstProfiles.SelectedIndex = profiles.Count - 1;
            lblProfileStatus.Text = "Perfil '" + pr.Name + "' creado.";
            SetStatus(lblProfileStatus.Text);
            Log("Perfil creado: " + pr.Name + " (" + pr.Exe + ").");
        }

        private void ApplySelectedProfile()
        {
            var pr = SelectedProfile();
            if (pr == null) { MessageBox.Show("Elige un perfil de la lista.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            ApplyProfileToTab(pr);
            SetGameMode(true);
            lblProfileStatus.Text = "Modo Juego activado con el perfil '" + pr.Name + "'.";
            SetStatus(lblProfileStatus.Text);
            Log("Perfil aplicado: " + pr.Name + ".");
        }

        private void LoadSelectedProfileIntoTab()
        {
            var pr = SelectedProfile();
            if (pr == null) return;
            ApplyProfileToTab(pr);
            SelectTab(Array.IndexOf(pages, tabJuego));
            SetStatus("Perfil '" + pr.Name + "' cargado en Modo Juego. Cambia lo que quieras y pulsa Guardar en Perfiles.");
        }

        private void SaveSelectedProfileFromTab()
        {
            var pr = SelectedProfile();
            if (pr == null) return;
            var updated = CurrentTabProfile(pr.Name, pr.Exe);
            int i = profiles.IndexOf(pr);
            profiles[i] = updated;
            SaveProfiles();
            ShowSelectedProfile();
            lblProfileStatus.Text = "Perfil '" + pr.Name + "' actualizado con los ajustes de Modo Juego.";
            SetStatus(lblProfileStatus.Text);
            Log("Perfil actualizado: " + pr.Name + ".");
        }

        private void RenameSelectedProfile()
        {
            var pr = SelectedProfile();
            if (pr == null) return;
            var name = Interaction.InputBox("Nuevo nombre del perfil:", "Renombrar perfil", pr.Name);
            if (string.IsNullOrWhiteSpace(name) || name.Trim() == pr.Name) return;
            pr.Name = name.Trim();
            SaveProfiles();
            RefreshProfileList();
            lblProfileStatus.Text = "Perfil renombrado a '" + pr.Name + "'.";
            SetStatus(lblProfileStatus.Text);
        }

        private void DeleteSelectedProfile()
        {
            var pr = SelectedProfile();
            if (pr == null) return;
            var r = MessageBox.Show("¿Borrar el perfil '" + pr.Name + "'? No toca nada de tu PC.",
                "GameBoost - Eliminar perfil", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            profiles.Remove(pr);
            SaveProfiles();
            RefreshProfileList();
            lblProfileStatus.Text = "Perfil '" + pr.Name + "' eliminado.";
            SetStatus(lblProfileStatus.Text);
            Log("Perfil eliminado: " + pr.Name + ".");
        }

        // ---------- Modo automatico ----------
        private void SetAutoMode(bool on)
        {
            autoModeOn = on;
            SaveProfiles();
            if (on)
            {
                StartAutoTimer();
                Log("Modo automatico activado: vigilando juegos con perfil.");
            }
            else
            {
                StopAutoTimer();
                if (autoActivated) { SetGameMode(false); autoActivated = false; autoGamePid = 0; }
                Log("Modo automatico desactivado.");
            }
            UpdateAutoStatus();
        }

        private void StartAutoTimer()
        {
            if (autoTimer == null)
            {
                autoTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                autoTimer.Tick += (_, _) => AutoTimerTick();
            }
            autoTimer.Start();
        }

        private void StopAutoTimer()
        {
            autoTimer?.Stop();
        }

        private static bool IsPidAlive(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch { return false; }
        }

        private void AutoTimerTick()
        {
            if (!autoModeOn) return;
            if (autoSuppressedPid != 0 && !IsPidAlive(autoSuppressedPid)) autoSuppressedPid = 0;

            if (gameModeOn)
            {
                // Si lo activo el automatico y el juego ya se cerro, desactivar.
                if (autoActivated && autoGamePid != 0 && !IsPidAlive(autoGamePid))
                {
                    Log("Modo automatico: el juego se cerro, desactivando Modo Juego.");
                    autoActivated = false; autoGamePid = 0; autoProfileName = "";
                    SetGameMode(false);
                    UpdateAutoStatus();
                }
                return;
            }

            // Modo Juego apagado: buscar un juego con perfil en ejecucion.
            foreach (var pr in profiles)
            {
                var baseName = ProfileStore.ExeBase(pr.Exe);
                if (string.IsNullOrEmpty(baseName)) continue;
                Process[] found;
                try { found = Process.GetProcessesByName(baseName); }
                catch { continue; }
                if (found.Length == 0) continue;
                int pid = found[0].Id;
                if (pid == autoSuppressedPid) continue;
                Log("Modo automatico: detectado " + pr.Name + ", activando Modo Juego.");
                ApplyProfileToTab(pr);
                autoActivated = true; autoGamePid = pid; autoProfileName = pr.Name;
                SetGameMode(true);
                lblProfileStatus.Text = "Modo automatico: Modo Juego activado por '" + pr.Name + "'.";
                UpdateAutoStatus();
                return;
            }
            UpdateAutoStatus();
        }

        private void UpdateAutoStatus()
        {
            if (lblAutoStatus == null) return;
            if (!autoModeOn)
            {
                lblAutoStatus.Text = "Modo automatico: apagado.";
                lblAutoStatus.ForeColor = TextMuted;
            }
            else if (autoActivated)
            {
                lblAutoStatus.Text = "Activado por " + autoProfileName + ": jugando ahora. Se apagara solo al cerrar el juego.";
                lblAutoStatus.ForeColor = Accent;
            }
            else if (autoSuppressedPid != 0)
            {
                lblAutoStatus.Text = "Lo apagaste a mano mientras jugabas: no se activara solo hasta que cierres y reabras el juego.";
                lblAutoStatus.ForeColor = Warn;
            }
            else
            {
                lblAutoStatus.Text = "Vigilando: esperando a que abras un juego con perfil...";
                lblAutoStatus.ForeColor = Accent;
            }
        }
    }
}
