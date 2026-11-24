using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Tab 5: Bloqueo de teclas ---
        private CheckBox kbWin = null!, kbWinD = null!, kbWinE = null!, kbWinR = null!, kbWinTab = null!;
        private CheckBox kbWinS = null!, kbWinI = null!, kbWinX = null!, kbAltTab = null!, kbCtrlEsc = null!;
        private Label lblKeyBlockStatus = null!, lblEmergencyCurrent = null!;
        private Button btnKeyOn = null!, btnKeyOff = null!;
        private CheckBox emCtrl = null!, emAlt = null!, emShift = null!, emWin = null!;
        private ComboBox cmbEmKey = null!;

        private void BuildTabBloqueo()
        {
            var p = tabBloqueo;
            p.Controls.Add(Lbl("Bloqueo de teclas: que nada te saque del juego por accidente.", 16, 12, 920, true));
            p.Controls.Add(Hint("Si alguna vez pulsaste la tecla Windows en plena partida y te saco al escritorio, esto es para ti. El bloqueo solo funciona mientras GameBoost este abierto.", 16, 34, 920));

            var card = Card(16, 60, 580, 340);
            card.Controls.Add(Lbl("Elige que teclas bloquear mientras juegas:", 12, 10, 540, true));
            kbWin = Chk("Tecla Windows sola  —  evita que se abra el menu Inicio por accidente", 12, 38, 540); Tip(kbWin, "Bloquea la tecla Windows (la del logo, a la izquierda del teclado). Es la que mas saca del juego sin querer."); card.Controls.Add(kbWin);
            kbWinD = Chk("Win + D  —  Mostrar escritorio", 12, 66, 540); Tip(kbWinD, "Evita que se minimice todo y aparezca el escritorio en plena partida."); card.Controls.Add(kbWinD);
            kbWinE = Chk("Win + E  —  Abrir el Explorador de archivos", 12, 94, 540); Tip(kbWinE, "Evita que se abra una ventana del Explorador encima del juego."); card.Controls.Add(kbWinE);
            kbWinR = Chk("Win + R  —  Abrir Ejecutar", 12, 122, 540); Tip(kbWinR, "Evita que se abra la ventanita Ejecutar."); card.Controls.Add(kbWinR);
            kbWinTab = Chk("Win + Tab  —  Vista de tareas", 12, 150, 540); Tip(kbWinTab, "Evita que se abra la vista de todas las ventanas abiertas."); card.Controls.Add(kbWinTab);
            kbWinS = Chk("Win + S  —  Buscar en Windows", 12, 178, 540); Tip(kbWinS, "Evita que se abra la busqueda de Windows."); card.Controls.Add(kbWinS);
            kbWinI = Chk("Win + I  —  Abrir Configuracion", 12, 206, 540); Tip(kbWinI, "Evita que se abra la Configuracion de Windows."); card.Controls.Add(kbWinI);
            kbWinX = Chk("Win + X  —  Menu rapido de Windows", 12, 234, 540); Tip(kbWinX, "Evita que se abra el menu rapido (el de clic derecho en Inicio)."); card.Controls.Add(kbWinX);
            kbAltTab = Chk("Alt + Tab  —  Cambiar de ventana", 12, 262, 540); Tip(kbAltTab, "Evita cambiar a otra ventana por accidente. Ojo: con esto activado no podras cambiar a Discord o al navegador con Alt+Tab hasta desactivarlo."); card.Controls.Add(kbAltTab);
            kbCtrlEsc = Chk("Ctrl + Esc  —  Abrir el menu Inicio", 12, 290, 540, false); Tip(kbCtrlEsc, "Otra forma de abrir el menu Inicio. Normalmente no hace falta bloquearla si ya bloqueaste la tecla Windows."); card.Controls.Add(kbCtrlEsc);
            p.Controls.Add(card);

            var card2 = Card(612, 60, 344, 340);
            card2.Controls.Add(Lbl("Antes de usarlo, lee esto:", 12, 10, 320, true));
            var info = new Label
            {
                Text = "• El bloqueo solo funciona mientras GameBoost este abierto. Al cerrar el programa, todo vuelve a la normalidad.\n\n• Win + L (bloquear sesion) y Ctrl + Alt + Supr NO se pueden bloquear: Windows los reserva por seguridad y ningun programa normal puede interceptarlos. No es un fallo de GameBoost.\n\n• Si necesitas salir rapido del juego, pulsa Desactivar bloqueo o cierra GameBoost.",
                Location = new Point(12, 38), Size = new Size(320, 220),
                ForeColor = TextMuted, Font = new Font("Segoe UI", 9f),
            };
            card2.Controls.Add(info);
            var btnHelp = Btn("Probar bloqueo (10 s)", 12, 268, 200, 36, AccentBlue);
            Tip(btnHelp, "Activa el bloqueo solo 10 segundos para probarlo sin quedarte bloqueado. Prueba pulsar la tecla Windows y veras que no pasa nada.");
            btnHelp.Click += async (_, _) =>
            {
                if (StartKeyBlockFromOptions())
                {
                    SetStatus("Bloqueo de prueba activado por 10 segundos...");
                    await System.Threading.Tasks.Task.Delay(10000);
                    KeyBlocker.Stop(Log);
                    UpdateKeyBlockStatus();
                    SetStatus("Prueba terminada, bloqueo desactivado.");
                }
            };
            card2.Controls.Add(btnHelp);
            p.Controls.Add(card2);

            btnKeyOn = Btn("🔒  ACTIVAR bloqueo", 16, 416, 260, 48, Accent);
            Tip(btnKeyOn, "Empieza a bloquear las teclas marcadas arriba. Funciona solo mientras GameBoost siga abierto.");
            btnKeyOn.Click += (_, _) => { if (StartKeyBlockFromOptions()) SetStatus("Bloqueo de teclas activado."); };
            btnKeyOff = Btn("🔓  DESACTIVAR bloqueo", 288, 416, 260, 48, Danger);
            Tip(btnKeyOff, "Vuelve a dejar todas las teclas de Windows funcionando normal.");
            btnKeyOff.Enabled = false;
            btnKeyOff.Click += (_, _) => { KeyBlocker.Stop(Log); UpdateKeyBlockStatus(); SetStatus("Bloqueo de teclas desactivado."); };
            p.Controls.AddRange(new Control[] { btnKeyOn, btnKeyOff });

            lblKeyBlockStatus = Lbl("Bloqueo de teclas: APAGADO", 16, 476, 940, true, TextMuted);
            p.Controls.Add(lblKeyBlockStatus);

            // --- Cierre de emergencia ---
            var cardE = Card(16, 512, 940, 190);
            cardE.Controls.Add(Lbl("Cerrar juego de emergencia (por si se congela)", 12, 10, 900, true));
            cardE.Controls.Add(Hint("Si el juego se congela y no responde ni con Alt+F4, pulsa tu combinacion y GameBoost cerrara el juego que elegiste en Modo Juego, sin reiniciar la PC.", 12, 34, 900));
            emCtrl = Chk("Ctrl", 12, 62, 70, true); cardE.Controls.Add(emCtrl);
            emAlt = Chk("Alt", 92, 62, 60, true); cardE.Controls.Add(emAlt);
            emShift = Chk("Shift", 162, 62, 70, false); cardE.Controls.Add(emShift);
            emWin = Chk("Win", 242, 62, 60, false); cardE.Controls.Add(emWin);
            cardE.Controls.Add(Lbl("+ tecla:", 322, 64, 60, true));
            cmbEmKey = new ComboBox { Location = new Point(382, 60), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Bg, ForeColor = TextLight };
            cmbEmKey.Items.AddRange(new object[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Esc", "Supr", "Fin", "Q", "X", "C", "K" });
            cmbEmKey.SelectedItem = "F12";
            Tip(cmbEmKey, "La tecla final de tu combinacion. F12 es buena porque casi ningun juego la usa.");
            cardE.Controls.Add(cmbEmKey);
            var btnSaveEm = Btn("💾  Guardar combinacion", 492, 56, 210, 36, AccentBlue);
            Tip(btnSaveEm, "Guarda tu combinacion para que se recuerde al abrir GameBoost. Funciona cuando el bloqueo esta ACTIVADO.");
            btnSaveEm.Click += (_, _) => SaveEmergencySettings();
            var btnKillNow = Btn("❌  Cerrar juego ahora", 714, 56, 210, 36, Danger);
            Tip(btnKillNow, "Cierra ahora mismo el juego elegido en Modo Juego. Usalo si ya se crasheo y no quieres esperar a la combinacion.");
            btnKillNow.Click += (_, _) => { var (_, msg) = GameModeService.KillGame(txtGameExe != null ? txtGameExe.Text.Trim() : "", Log); SetStatus(msg); };
            cardE.Controls.AddRange(new Control[] { btnSaveEm, btnKillNow });
            lblEmergencyCurrent = Lbl("Combinacion actual: Ctrl + Alt + F12", 12, 104, 900, true, Accent);
            cardE.Controls.Add(lblEmergencyCurrent);
            cardE.Controls.Add(Hint("Para que funcione, el bloqueo debe estar ACTIVADO arriba (el hook solo escucha mientras GameBoost esta abierto). Si no elegiste ningun juego en Modo Juego, la combinacion no hara nada. No cierra Windows ni otros programas, solo tu juego.", 12, 130, 900));
            p.Controls.Add(cardE);
            LoadEmergencySettings();
        }

        internal void LoadEmergencySettings()
        {
            var s = AppSettings.Load();
            if (emCtrl != null) emCtrl.Checked = s.EmCtrl;
            if (emAlt != null) emAlt.Checked = s.EmAlt;
            if (emShift != null) emShift.Checked = s.EmShift;
            if (emWin != null) emWin.Checked = s.EmWin;
            if (cmbEmKey != null) cmbEmKey.SelectedItem = s.EmKey;
            if (lblEmergencyCurrent != null) lblEmergencyCurrent.Text = "Combinacion actual: " + s.Display;
            KeyBlocker.EmergencyCtrl = s.EmCtrl;
            KeyBlocker.EmergencyAlt = s.EmAlt;
            KeyBlocker.EmergencyShift = s.EmShift;
            KeyBlocker.EmergencyWin = s.EmWin;
            KeyBlocker.EmergencyKeyVk = KeyboardBlocker.KeyNameToVk(s.EmKey);
        }

        internal void SaveEmergencySettings()
        {
            var s = new AppSettings
            {
                EmCtrl = emCtrl.Checked, EmAlt = emAlt.Checked, EmShift = emShift.Checked, EmWin = emWin.Checked,
                EmKey = cmbEmKey.SelectedItem?.ToString() ?? "F12",
            };
            s.Save();
            LoadEmergencySettings();
            SetStatus("Combinacion de emergencia guardada: " + s.Display);
            Log("Combinacion de emergencia guardada: " + s.Display + ". Recuerda: funciona cuando el bloqueo esta ACTIVADO.");
        }

        internal bool StartKeyBlockFromOptions()
        {
            var opts = new KeyboardBlocker.Options
            {
                BlockWinKey = kbWin != null && kbWin.Checked,
                BlockWinD = kbWinD != null && kbWinD.Checked,
                BlockWinE = kbWinE != null && kbWinE.Checked,
                BlockWinR = kbWinR != null && kbWinR.Checked,
                BlockWinTab = kbWinTab != null && kbWinTab.Checked,
                BlockWinS = kbWinS != null && kbWinS.Checked,
                BlockWinI = kbWinI != null && kbWinI.Checked,
                BlockWinX = kbWinX != null && kbWinX.Checked,
                BlockAltTab = kbAltTab != null && kbAltTab.Checked,
                BlockCtrlEsc = kbCtrlEsc != null && kbCtrlEsc.Checked,
            };
            KeyBlocker.EmergencyCtrl = emCtrl != null && emCtrl.Checked;
            KeyBlocker.EmergencyAlt = emAlt != null && emAlt.Checked;
            KeyBlocker.EmergencyShift = emShift != null && emShift.Checked;
            KeyBlocker.EmergencyWin = emWin != null && emWin.Checked;
            KeyBlocker.EmergencyKeyVk = KeyboardBlocker.KeyNameToVk(cmbEmKey != null && cmbEmKey.SelectedItem != null ? cmbEmKey.SelectedItem.ToString()! : "F12");
            bool ok = KeyBlocker.Start(opts, Log);
            UpdateKeyBlockStatus();
            return ok;
        }

        internal void UpdateKeyBlockStatus()
        {
            if (lblKeyBlockStatus == null) return;
            if (KeyBlocker.IsActive)
            {
                lblKeyBlockStatus.Text = "Bloqueo de teclas: ACTIVADO  ✓  —  las teclas marcadas no te sacaran del juego. Se apaga solo al cerrar GameBoost.";
                lblKeyBlockStatus.ForeColor = Accent;
                if (btnKeyOn != null) btnKeyOn.Enabled = false;
                if (btnKeyOff != null) btnKeyOff.Enabled = true;
            }
            else
            {
                lblKeyBlockStatus.Text = "Bloqueo de teclas: APAGADO";
                lblKeyBlockStatus.ForeColor = TextMuted;
                if (btnKeyOn != null) btnKeyOn.Enabled = true;
                if (btnKeyOff != null) btnKeyOff.Enabled = false;
            }
        }
    }
}
