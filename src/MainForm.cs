using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm : Form
    {
        // Colores profesionales v1.2 (tema oscuro, verde GameBoost)
        private static readonly Color Bg = Theme.Bg;
        private static readonly Color PanelBg = Theme.PanelBg;
        private static readonly Color CardBg = Theme.CardBg;
        private static readonly Color Accent = Theme.Accent;
        private static readonly Color AccentBlue = Theme.AccentBlue;
        private static readonly Color Warn = Theme.Warn;
        private static readonly Color Danger = Theme.Danger;
        private static readonly Color TextLight = Theme.TextLight;
        private static readonly Color TextMuted = Theme.TextMuted;

        private TextBox logBox = null!;
        private Label statusLabel = null!;
        private Label adminLabel = null!;
        private ToolTip tips = null!;
        internal KeyboardBlocker KeyBlocker = new KeyboardBlocker();

        // --- Medidor ---
        private ComboBox cmbTarget = null!;
        private TextBox txtCustom = null!;
        private Button btnStartPing = null!, btnStopPing = null!;
        private Label lblCurrent = null!, lblAvg = null!, lblMinMax = null!, lblJitter = null!, lblLoss = null!, lblVerdict = null!, lblSamples = null!;
        private Label lblBefore = null!, lblAfter = null!;
        private Panel graphPanel = null!;
        private System.Windows.Forms.Timer pingTimer = null!;
        private List<long> pingHistory = new();
        private List<long> pingOkAll = new();
        private int pingTotal, pingLost;
        private bool pingRunning;
        private double? beforeAvg, afterAvg;
        private string CurrentTarget => string.IsNullOrWhiteSpace(txtCustom.Text) ? TargetFromCombo() : txtCustom.Text.Trim();

        private string TargetFromCombo()
        {
            var s = cmbTarget.SelectedItem?.ToString() ?? "1.1.1.1";
            return s.Split(' ')[0];
        }

        public MainForm()
        {
            Text = "GameBoost v1.4  -  Optimizacion de red para juegos online  |  Nous Logic";
            Width = 1040; Height = 860;
            MinimumSize = new Size(980, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg; ForeColor = TextLight;
            Font = new Font("Segoe UI", 9.5f);
            TryLoadIcon();
            tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };
            FormClosing += (_, _) => KeyBlocker.Dispose();

            BuildShell();
            BuildTabMedidor();
            BuildTabServidores();
            BuildTabModoJuego();
            BuildTabOptimizacion();
            BuildTabBloqueo();
            BuildTabMiPc();
            BuildTabOptimizar();
            BuildTabDonaciones();
            KeyBlocker.EmergencyCloseRequested += OnEmergencyClose;
            RefreshSystemInfo();

            pingTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            pingTimer.Tick += async (_, _) => await DoOnePingAsync();

            Log("GameBoost v1.4 iniciado. Creado por Nous Logic - nouslogic.xyz");
            Log("Aviso honesto: esto NO aumenta tu velocidad contratada ni acerca los servidores. Mejora lo que depende de tu PC: fondo, prioridad, TCP/DNS y eleccion de servidor.");
            Log("Consejo: pasa el mouse por encima de cada boton o campo para ver que hace, en palabras simples.");
            if (!SystemInfoHelper.IsAdmin())
            {
                adminLabel.Text = "⚠ NO eres administrador: la optimizacion TCP/DNS y el Modo Juego no se podran aplicar. Cierra y abre GameBoost como administrador.";
                adminLabel.ForeColor = Warn;
                Log("ADVERTENCIA: sin permisos de administrador, netsh y el registro fallaran.");
            }
            else
            {
                adminLabel.Text = "✓ Ejecutando como administrador";
                adminLabel.ForeColor = Accent;
            }
        }

        // ---------- Logo ----------
        private void TryLoadIcon()
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                using var s = asm.GetManifestResourceStream("GameBoost.logo.ico");
                if (s != null) Icon = new Icon(s);
            }
            catch { /* sin icono incrustado: se sigue con el de Windows */ }
        }

        private Image? LoadLogoImage()
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                using var s = asm.GetManifestResourceStream("GameBoost.logo.png");
                if (s != null) return Image.FromStream(s);
            }
            catch { }
            return null;
        }

        // ---------- Shell ----------
        private TabControl tabs = null!;
        private TabPage tabMedidor = null!, tabServidores = null!, tabJuego = null!, tabOpt = null!, tabDonaciones = null!, tabBloqueo = null!, tabMiPc = null!, tabOptimizar = null!;
        private Panel headerPanel = null!, bottomPanel = null!;
        private Label honestLabel = null!;

        private void BuildShell()
        {
            headerPanel = new Panel { Location = new Point(0, 0), Size = new Size(ClientSize.Width, 82), BackColor = PanelBg, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var logoBox = new PictureBox { Location = new Point(14, 11), Size = new Size(60, 60), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            var logoImg = LoadLogoImage();
            if (logoImg != null) logoBox.Image = logoImg;
            var title = new Label { Text = "GameBoost", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.White, Location = new Point(84, 8), AutoSize = true };
            var sub = new Label { Text = "Optimizacion de red para juegos online  •  Ping en vivo  •  Modo Juego  •  Bloqueo de teclas", ForeColor = TextMuted, Location = new Point(86, 48), AutoSize = true };
            adminLabel = new Label { Text = "Comprobando permisos...", ForeColor = TextMuted, Location = new Point(640, 14), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var ver = new Label { Text = "v1.2  •  Nous Logic", ForeColor = TextMuted, Location = new Point(640, 38), AutoSize = true };
            var aboutBtn = Btn("Acerca de", 850, 18, 150, 36, AccentBlue);
            aboutBtn.Click += (_, _) => MessageBox.Show(
                "GameBoost v1.4\nOptimizacion de red para juegos online.\n\nCreado por Nous Logic - nouslogic.xyz\n\nLo que hace: mide tu ping real, prueba servidores, cierra apps que roban red, prioriza tu juego, bloquea la tecla Windows mientras juegas, analiza tu PC y sus cuellos de botella, y aplica ajustes TCP/IP y DNS reversibles.\n\nLo que NO hace: no aumenta la velocidad de tu internet, no baja el ping por arte de magia ni acerca servidores lejanos. Si un programa te promete eso, desconfia.\n\nSi te sirve, puedes apoyar el desarrollo en la pestana Donaciones (PayPal o Buy Me a Coffee). Es voluntario, todo sigue gratis.",
                "Acerca de GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information);
            var accentLine = new Panel { Location = new Point(0, 79), Size = new Size(ClientSize.Width, 3), BackColor = Accent, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            headerPanel.Controls.AddRange(new Control[] { logoBox, title, sub, adminLabel, ver, aboutBtn, accentLine });
            Controls.Add(headerPanel);

            honestLabel = new Label
            {
                Location = new Point(0, 82), Size = new Size(ClientSize.Width, 30), BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.FromArgb(253, 224, 71),
                Text = "   💡  Regla de oro: si juegas por Wi-Fi, usa cable Ethernet. Es el cambio que mas baja el ping y el jitter.",
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(honestLabel);

            tabs = new TabControl { Location = new Point(0, 112), Size = new Size(ClientSize.Width, ClientSize.Height - 112 - 150), BackColor = Bg, ForeColor = TextLight, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            tabMedidor = new TabPage("📶  Medidor") { BackColor = Bg };
            tabServidores = new TabPage("🌎  Servidores") { BackColor = Bg };
            tabJuego = new TabPage("🎮  Modo Juego") { BackColor = Bg };
            tabOpt = new TabPage("⚙️  Optimizacion") { BackColor = Bg };
            tabBloqueo = new TabPage("🔒  Teclas") { BackColor = Bg };
            tabMiPc = new TabPage("💻  Mi PC") { BackColor = Bg };
            tabOptimizar = new TabPage("🧹  Optimizar") { BackColor = Bg };
            tabDonaciones = new TabPage("💛  Donaciones") { BackColor = Bg };
            // Scroll vertical y horizontal en todas las pestanas: si el contenido
            // no cabe (pantalla chica, escala de Windows al 125/150%), aparecen
            // las barras y se puede bajar hasta el ultimo boton.
            foreach (var pg in new[] { tabMedidor, tabServidores, tabJuego, tabOpt, tabBloqueo, tabMiPc, tabOptimizar, tabDonaciones })
            { pg.AutoScroll = true; pg.AutoScrollMinSize = new Size(960, 620); }
            tabs.TabPages.AddRange(new[] { tabMedidor, tabServidores, tabJuego, tabOpt, tabBloqueo, tabMiPc, tabOptimizar, tabDonaciones });
            Theme.StyleTabs(tabs);
            Controls.Add(tabs);

            bottomPanel = new Panel { Location = new Point(0, ClientSize.Height - 150), Size = new Size(ClientSize.Width, 150), BackColor = PanelBg, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            statusLabel = new Label { Text = "Listo.", Location = new Point(10, 4), Size = new Size(ClientSize.Width - 20, 22), ForeColor = TextMuted };
            logBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(8, 26), Size = new Size(ClientSize.Width - 16, 116), BackColor = Color.FromArgb(15, 20, 32), ForeColor = Color.FromArgb(203, 213, 225), Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(logBox);
            Controls.Add(bottomPanel);

            Resize += (_, _) => LayoutShell();
        }

        private void LayoutShell()
        {
            if (headerPanel == null) return;
            headerPanel.Width = ClientSize.Width;
            honestLabel.Width = ClientSize.Width;
            bottomPanel.Location = new Point(0, ClientSize.Height - 150);
            bottomPanel.Width = ClientSize.Width;
            tabs.Size = new Size(ClientSize.Width, Math.Max(200, ClientSize.Height - 112 - 150));
            statusLabel.Width = ClientSize.Width - 20;
        }

        // ---------- Helpers UI ----------
        private Button Btn(string text, int x, int y, int w, int h, Color? bg = null)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = bg ?? Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            // Hover sutil: aclara el boton al pasar el mouse, para que se sienta profesional.
            var baseColor = b.BackColor;
            b.MouseEnter += (_, _) => { if (b.Enabled) b.BackColor = ControlPaint.Light(baseColor, 0.15f); };
            b.MouseLeave += (_, _) => { b.BackColor = baseColor; };
            return b;
        }
        private Label Lbl(string text, int x, int y, int w = 400, bool bold = false, Color? c = null)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 22), ForeColor = c ?? TextLight, Font = new Font("Segoe UI", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular), AutoSize = false };
        }
        /// <summary>Texto pequeno en gris, para explicar un campo o boton en palabras simples.</summary>
        private Label Hint(string text, int x, int y, int w = 400)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 20), ForeColor = TextMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), AutoSize = false };
        }
        private void Tip(Control c, string text)
        {
            if (tips != null) tips.SetToolTip(c, text);
        }
        private CheckBox Chk(string text, int x, int y, int w = 420, bool check = true)
        {
            return new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(w, 24), ForeColor = TextLight, Checked = check, BackColor = Color.Transparent };
        }
        private Panel Card(int x, int y, int w, int h)
        {
            var p = new RoundedPanel { Location = new Point(x, y), Size = new Size(w, h), BackColor = CardBg, BorderColor = Theme.CardBorder, CornerRadius = 12 };
            return p;
        }
        /// <summary>Tarjeta de seccion con titulo y descripcion amigable.</summary>
        private Panel SectionCard(int x, int y, int w, int h, string title, string description)
        {
            var p = Card(x, y, w, h);
            p.Controls.Add(Lbl(title, 14, 12, w - 28, true));
            var d = new Label { Text = description, Location = new Point(14, 38), Size = new Size(w - 28, 36), ForeColor = TextMuted, Font = new Font("Segoe UI", 9f) };
            p.Controls.Add(d);
            return p;
        }
        // Cierre de emergencia: se ejecuta en el hilo de la UI aunque venga del hook
        private void OnEmergencyClose()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(OnEmergencyClose)); return; }
            var (closed, msg) = GameModeService.KillGame(txtGameExe != null ? txtGameExe.Text.Trim() : "", Log);
            SetStatus(msg);
            MessageBox.Show(msg, "GameBoost - Cierre de emergencia", MessageBoxButtons.OK, closed > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void Log(string msg)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action<string>(Log), msg); return; }
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }
        private void SetStatus(string s)
        {
            if (statusLabel.InvokeRequired) { statusLabel.Invoke(new Action<string>(SetStatus), s); return; }
            statusLabel.Text = s;
        }

        // ---------- Tab 1: Medidor ----------
        private void BuildTabMedidor()
        {
            var p = tabMedidor;
            p.Controls.Add(Lbl("Mide tu ping en vivo, antes y despues de optimizar, para ver con numeros si de verdad mejoro.", 16, 12, 920, true));
            p.Controls.Add(Hint("El ping es el tiempo que tarda tu PC en hablar con el servidor. Menos ms = mejor. 1.1.1.1 y 8.8.8.8 siempre responden; un servidor de juego puede bloquear el ping.", 16, 34, 920));
            p.Controls.Add(Lbl("Destino:", 16, 62, 70, true));
            cmbTarget = new ComboBox { Location = new Point(90, 60), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CardBg, ForeColor = TextLight };
            cmbTarget.Items.AddRange(new object[] { "1.1.1.1  - Cloudflare", "8.8.8.8  - Google", "208.67.222.222  - OpenDNS", "9.9.9.9  - Quad9" });
            cmbTarget.SelectedIndex = 0;
            Tip(cmbTarget, "El servidor al que se le mide el ping cada segundo. Empieza con 1.1.1.1, que siempre responde.");
            p.Controls.Add(cmbTarget);
            p.Controls.Add(Lbl("o personalizado:", 424, 62, 120));
            txtCustom = new TextBox { Location = new Point(548, 60), Width = 220, BackColor = CardBg, ForeColor = TextLight, PlaceholderText = "ej. 104.160.131.3 o tu servidor" };
            Tip(txtCustom, "Si conoces la IP de tu servidor de juego, escribela aqui y se medira esa en vez de la lista.");
            p.Controls.Add(txtCustom);

            btnStartPing = Btn("▶  Iniciar medicion", 16, 96, 190, 38, Accent);
            Tip(btnStartPing, "Empieza a medir el ping cada segundo y dibuja la grafica. Dejalo correr al menos 30 segundos.");
            btnStartPing.Click += (_, _) => StartPing();
            btnStopPing = Btn("■  Detener", 216, 96, 120, 38, Danger);
            Tip(btnStopPing, "Para la medicion. Los numeros quedan en pantalla para compararlos.");
            btnStopPing.Enabled = false;
            btnStopPing.Click += (_, _) => StopPing();
            var btnBefore = Btn("Marcar ANTES", 348, 96, 150, 38, AccentBlue);
            Tip(btnBefore, "Guarda el promedio actual como tu punto de partida, antes de optimizar.");
            btnBefore.Click += (_, _) => { beforeAvg = pingOkAll.Count > 0 ? pingOkAll.Average() : null; lblBefore.Text = beforeAvg.HasValue ? $"ANTES: {beforeAvg:F1} ms" : "ANTES: mide primero unos segundos"; Log("Marca ANTES guardada: " + lblBefore.Text); };
            var btnAfter = Btn("Marcar DESPUES", 510, 96, 170, 38, AccentBlue);
            Tip(btnAfter, "Guarda el promedio despues de optimizar y te dice si mejoro, empeoro o no cambio nada.");
            btnAfter.Click += (_, _) => {
                afterAvg = pingOkAll.Count > 0 ? pingOkAll.Average() : null;
                lblAfter.Text = afterAvg.HasValue ? $"DESPUES: {afterAvg:F1} ms" : "DESPUES: mide primero unos segundos";
                if (beforeAvg.HasValue && afterAvg.HasValue)
                {
                    double diff = beforeAvg.Value - afterAvg.Value;
                    lblAfter.Text += diff > 1 ? $"   ↓ mejoro {diff:F1} ms" : diff < -1 ? $"   ↑ empeoro {-diff:F1} ms" : "   = sin cambio real";
                }
                Log("Marca DESPUES guardada: " + lblAfter.Text);
            };
            p.Controls.AddRange(new Control[] { btnStartPing, btnStopPing, btnBefore, btnAfter });

            var card = Card(16, 146, 940, 150);
            lblCurrent = new Label { Text = "— ms", Font = new Font("Segoe UI", 32, FontStyle.Bold), ForeColor = Color.White, Location = new Point(16, 10), Size = new Size(220, 55) };
            lblVerdict = new Label { Text = "Pulsa Iniciar medicion", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Accent, Location = new Point(16, 70), Size = new Size(280, 26) };
            lblAvg = Lbl("Promedio: —", 320, 18, 280);
            lblMinMax = Lbl("Min / Max: —", 320, 46, 280);
            lblJitter = Lbl("Jitter: —", 320, 74, 280);
            lblLoss = Lbl("Perdida: —", 320, 102, 280);
            lblSamples = Lbl("Muestras: 0", 620, 18, 280);
            lblBefore = Lbl("ANTES: sin medir", 620, 46, 300, false, TextMuted);
            lblAfter = Lbl("DESPUES: sin medir", 620, 74, 300, false, TextMuted);
            var hint = Lbl("Consejo: mide 30 s antes de optimizar y 30 s despues, al mismo destino.", 620, 102, 300, false, TextMuted);
            card.Controls.AddRange(new Control[] { lblCurrent, lblVerdict, lblAvg, lblMinMax, lblJitter, lblLoss, lblSamples, lblBefore, lblAfter, hint });
            p.Controls.Add(card);

            p.Controls.Add(Lbl("Grafica de ping (ultimos 60 s):", 16, 308, 400, true));
            graphPanel = new Panel { Location = new Point(16, 334), Size = new Size(940, 120), BackColor = Color.FromArgb(15, 20, 32), BorderStyle = BorderStyle.FixedSingle };
            graphPanel.Paint += GraphPanel_Paint;
            p.Controls.Add(graphPanel);

            p.Controls.Add(Lbl("Que significa:  <30 ms Excelente  •  30-60 Bueno  •  60-100 Regular  •  >100 Malo.  El jitter (variacion) importa tanto como el ping: si sube y baja mucho, el juego se siente con tirones aunque el promedio se vea bien.", 16, 464, 940, false, TextMuted));
        }

        private void GraphPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var w = graphPanel.Width; var h = graphPanel.Height;
            using var gridPen = new Pen(Color.FromArgb(40, 50, 70));
            for (int y = 30; y < h; y += 30) g.DrawLine(gridPen, 0, y, w, y);
            if (pingHistory.Count < 2) { g.DrawString("Sin datos todavia", Font, Brushes.Gray, 10, 10); return; }
            long max = Math.Max(100, pingHistory.Max());
            var pts = new List<Point>();
            for (int i = 0; i < pingHistory.Count; i++)
            {
                int x = (int)(i * (w - 10) / 59.0) + 5;
                int y = h - 8 - (int)(pingHistory[i] * (h - 16) / (double)max);
                pts.Add(new Point(x, Math.Max(4, y)));
            }
            using var pen = new Pen(Accent, 2);
            g.DrawLines(pen, pts.ToArray());
        }

        private void StartPing()
        {
            pingRunning = true; pingHistory.Clear(); pingOkAll.Clear(); pingTotal = 0; pingLost = 0;
            btnStartPing.Enabled = false; btnStopPing.Enabled = true;
            pingTimer.Start();
            SetStatus("Midiendo ping a " + CurrentTarget + " cada segundo...");
            Log("Medicion iniciada a " + CurrentTarget);
        }
        private void StopPing()
        {
            pingRunning = false; pingTimer.Stop();
            btnStartPing.Enabled = true; btnStopPing.Enabled = false;
            SetStatus("Medicion detenida.");
            Log("Medicion detenida.");
        }

        private async Task DoOnePingAsync()
        {
            if (!pingRunning) return;
            string target = CurrentTarget;
            var s = await PingService.PingOnceAsync(target, 1000);
            pingTotal++;
            if (s.Ok && s.Ms.HasValue)
            {
                pingOkAll.Add(s.Ms.Value);
                pingHistory.Add(s.Ms.Value);
                if (pingHistory.Count > 60) pingHistory.RemoveAt(0);
                lblCurrent.Text = s.Ms.Value + " ms";
            }
            else
            {
                pingLost++;
                pingHistory.Add(1000);
                if (pingHistory.Count > 60) pingHistory.RemoveAt(0);
                lblCurrent.Text = "sin respuesta";
            }
            if (pingOkAll.Count > 0)
            {
                var stats = PingService.ComputeFromMs(pingOkAll, pingTotal);
                lblAvg.Text = $"Promedio: {stats.Avg:F1} ms";
                lblMinMax.Text = $"Min / Max: {stats.Min} / {stats.Max} ms";
                lblJitter.Text = $"Jitter: {stats.Jitter:F1} ms";
                lblLoss.Text = $"Perdida: {stats.LossPct:F1}%  ({stats.Lost}/{stats.Total})";
                lblSamples.Text = $"Muestras: {stats.Total}  (buenas: {stats.OkCount})";
                lblVerdict.Text = stats.Verdict;
                lblVerdict.ForeColor = stats.Avg < 30 ? Accent : stats.Avg < 60 ? Color.FromArgb(96, 165, 250) : stats.Avg < 100 ? Warn : Danger;
            }
            else
            {
                lblLoss.Text = $"Perdida: 100%  ({pingLost}/{pingTotal})";
                lblVerdict.Text = "Sin respuesta: el destino bloquea el ping o no hay internet";
                lblVerdict.ForeColor = Danger;
            }
            graphPanel.Invalidate();
        }
    }
}
