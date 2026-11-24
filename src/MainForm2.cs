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
        // --- Tab 2 ---
        private ListView lvServers = null!;
        private Button btnTestAll = null!;
        private TextBox txtCustomServer = null!;
        private Label lblBest = null!;

        private readonly List<(string Name, string Host)> serverPresets = new()
        {
            ("Cloudflare DNS", "1.1.1.1"),
            ("Google DNS", "8.8.8.8"),
            ("OpenDNS", "208.67.222.222"),
            ("Quad9 DNS", "9.9.9.9"),
            ("Steam", "store.steampowered.com"),
            ("Epic Games / Fortnite", "epicgames.com"),
            ("Riot Games / Valorant-LoL", "riotgames.com"),
            ("Minecraft", "minecraft.net"),
            ("League of Legends NA*", "104.160.131.3"),
        };

        private void BuildTabServidores()
        {
            var p = tabServidores;
            p.Controls.Add(Lbl("Prueba que servidor te responde mas rapido, para elegir la mejor region en tu juego.", 16, 12, 920, true));
            p.Controls.Add(Hint("Probamos cada servidor 4 veces y te los ordenamos del mas rapido al mas lento. *La IP de LoL NA puede cambiar; si no responde, Riot la movio.", 16, 34, 920));
            p.Controls.Add(Lbl("Si un servidor de juego marca 100% perdida, casi siempre es porque bloquea el ping (ICMP), no porque este caido. Prueba con su pagina web.", 16, 36, 920, false, TextMuted));

            lvServers = new ListView { Location = new Point(16, 66), Size = new Size(940, 280), View = View.Details, FullRowSelect = true, GridLines = true, BackColor = CardBg, ForeColor = TextLight };
            lvServers.Columns.Add("Servidor", 240);
            lvServers.Columns.Add("Host", 220);
            lvServers.Columns.Add("Min", 80);
            lvServers.Columns.Add("Promedio", 90);
            lvServers.Columns.Add("Max", 80);
            lvServers.Columns.Add("Perdida", 80);
            lvServers.Columns.Add("Veredicto", 140);
            foreach (var (name, host) in serverPresets)
                lvServers.Items.Add(new ListViewItem(new[] { name, host, "-", "-", "-", "-", "Sin probar" }));
            p.Controls.Add(lvServers);

            btnTestAll = Btn("▶  Probar todos los servidores", 16, 360, 280, 40, Accent);
            Tip(btnTestAll, "Mide 4 veces cada servidor de la lista y te marca cual es el mas rapido y sin perdida.");
            btnTestAll.Click += async (_, _) => await TestAllServersAsync();
            p.Controls.Add(btnTestAll);
            p.Controls.Add(Lbl("Servidor personalizado:", 320, 372, 170, true));
            txtCustomServer = new TextBox { Location = new Point(492, 370), Width = 220, BackColor = CardBg, ForeColor = TextLight, PlaceholderText = "IP o dominio" };
            p.Controls.Add(txtCustomServer);
            var btnAdd = Btn("Agregar y probar", 724, 366, 160, 38, AccentBlue);
            btnAdd.Click += async (_, _) =>
            {
                var h = txtCustomServer.Text.Trim();
                if (string.IsNullOrWhiteSpace(h)) { MessageBox.Show("Escribe una IP o dominio.", "GameBoost"); return; }
                var item = lvServers.Items.Add(new ListViewItem(new[] { "Personalizado", h, "-", "-", "-", "-", "Probando..." }));
                var st = await PingService.PingManyAsync(h, 4);
                FillServerRow(item, st);
            };
            p.Controls.Add(btnAdd);

            lblBest = Lbl("Mejor servidor: sin probar todavia.", 16, 416, 940, true, Accent);
            p.Controls.Add(lblBest);
            p.Controls.Add(Lbl("Como usarlo: elige el servidor con el promedio mas bajo y 0% de perdida. En el juego, elige a mano la region o servidor mas cercano a ese resultado; no lo dejes en automatico si te manda lejos.", 16, 446, 940, false, TextMuted, 40));
        }

        private void FillServerRow(ListViewItem item, PingStats st)
        {
            if (st.Total == 0 || st.OkCount == 0)
            {
                item.SubItems[2].Text = "-"; item.SubItems[3].Text = "-"; item.SubItems[4].Text = "-";
                item.SubItems[5].Text = "100%"; item.SubItems[6].Text = "Sin respuesta";
            }
            else
            {
                item.SubItems[2].Text = st.Min + " ms";
                item.SubItems[3].Text = st.Avg.ToString("F1") + " ms";
                item.SubItems[4].Text = st.Max + " ms";
                item.SubItems[5].Text = st.LossPct.ToString("F1") + "%";
                item.SubItems[6].Text = st.Verdict;
            }
        }

        private async Task TestAllServersAsync()
        {
            btnTestAll.Enabled = false;
            SetStatus("Probando servidores... (4 pings cada uno)");
            Log("Test de servidores iniciado.");
            string bestName = ""; double bestAvg = double.MaxValue;
            for (int i = 0; i < lvServers.Items.Count; i++)
            {
                var item = lvServers.Items[i];
                string host = item.SubItems[1].Text;
                item.SubItems[6].Text = "Probando...";
                var st = await PingService.PingManyAsync(host, 4);
                FillServerRow(item, st);
                Log($"{item.SubItems[0].Text} ({host}): prom { (st.OkCount > 0 ? st.Avg.ToString("F1") + " ms" : "sin respuesta")}, perdida {st.LossPct:F1}%");
                if (st.OkCount > 0 && st.LossPct == 0 && st.Avg < bestAvg) { bestAvg = st.Avg; bestName = item.SubItems[0].Text + " (" + host + ")"; }
            }
            lblBest.Text = bestName == "" ? "Mejor servidor: ninguno respondio con 0% de perdida. Revisa tu internet." : $"Mejor servidor: {bestName}  —  {bestAvg:F1} ms promedio, 0% perdida.";
            Log(lblBest.Text);
            SetStatus("Test terminado.");
            btnTestAll.Enabled = true;
        }

        // --- Tab 3: Modo Juego ---
        private CheckBox chkChrome = null!, chkEdge = null!, chkFirefox = null!, chkOneDrive = null!, chkSpotify = null!, chkDiscord = null!;
        private CheckBox chkUpdate = null!, chkPower = null!, chkCloseApps = null!, chkAutoBlock = null!;
        private TextBox txtGameExe = null!;
        private Label lblGameStatus = null!, lblPriorityInfo = null!;
        private Button btnGameOn = null!, btnGameOff = null!;

        private void BuildTabModoJuego()
        {
            var p = tabJuego;
            p.Controls.Add(Lbl("Modo Juego: prepara tu PC para jugar sin interrupciones.", 16, 12, 920, true));
            p.Controls.Add(Hint("Cierra las apps que roban internet, pone tu PC en Alto rendimiento, pausa Windows Update y le da prioridad a tu juego. Al desactivarlo, todo vuelve a como estaba.", 16, 34, 920));

            var card1 = Card(16, 60, 460, 300);
            card1.Controls.Add(Lbl("Al activar el Modo Juego, cerramos estas apps:", 12, 10, 400, true));
            chkCloseApps = Chk("Cerrar las apps marcadas abajo", 12, 36, 400); card1.Controls.Add(chkCloseApps);
            chkChrome = Chk("Google Chrome  (chrome.exe)", 12, 64, 400); card1.Controls.Add(chkChrome);
            chkEdge = Chk("Microsoft Edge  (msedge.exe)", 12, 90, 400); card1.Controls.Add(chkEdge);
            chkFirefox = Chk("Firefox  (firefox.exe)", 12, 116, 400); card1.Controls.Add(chkFirefox);
            chkOneDrive = Chk("OneDrive  (OneDrive.exe)", 12, 142, 400); card1.Controls.Add(chkOneDrive);
            chkSpotify = Chk("Spotify  (Spotify.exe)", 12, 168, 400); card1.Controls.Add(chkSpotify);
            chkDiscord = Chk("Discord  (Discord.exe)  —  NO recomendado si lo usas para hablar", 12, 194, 430, false); card1.Controls.Add(chkDiscord);
            card1.Controls.Add(Hint("GameBoost nunca cierra el juego que elijas.", 12, 226, 430));
            card1.Controls.Add(Hint("Ojo: cerrar Chrome o Edge cierra tus pestanas. Guarda tu trabajo antes.", 12, 250, 430));
            p.Controls.Add(card1);

            var card2 = Card(492, 60, 464, 300);
            card2.Controls.Add(Lbl("Que cambia Windows al activar el Modo Juego:", 12, 10, 400, true));
            chkPower = Chk("Plan de energia: Alto rendimiento", 12, 36, 420); Tip(chkPower, "Tu PC usa toda su potencia mientras juegas, sin ahorrar bateria ni bajar la velocidad del procesador."); card2.Controls.Add(chkPower);
            chkUpdate = Chk("Pausar Windows Update mientras juegas", 12, 64, 420); Tip(chkUpdate, "Evita que Windows descargue actualizaciones en plena partida y te suba el ping. Se reanuda al desactivar el Modo Juego."); card2.Controls.Add(chkUpdate);
            chkAutoBlock = Chk("Bloquear tecla Windows al activar Modo Juego", 12, 90, 420, false); Tip(chkAutoBlock, "Si lo marcas, al activar el Modo Juego tambien se activa el bloqueo de teclas del menu Teclas, con lo que tengas marcado ahi."); card2.Controls.Add(chkAutoBlock);
            card2.Controls.Add(Lbl("Tu juego (opcional): prioridad Alta en Windows", 12, 120, 420, true));
            txtGameExe = new TextBox { Location = new Point(12, 148), Width = 420, BackColor = Bg, ForeColor = TextLight, PlaceholderText = "ej. FortniteClient-Win64-Shipping.exe" };
            Tip(txtGameExe, "El .exe de tu juego. No lo escribas a mano si no quieres: usa Elegir mi juego abierto o Buscar el .exe del juego.");
            card2.Controls.Add(txtGameExe);
            var btnPick = Btn("🔍  Elegir mi juego abierto", 12, 184, 200, 34, AccentBlue);
            Tip(btnPick, "Abre el juego primero y elijelo de la lista de programas que estan corriendo. Es la forma mas facil y sin errores.");
            btnPick.Click += (_, _) => PickGameProcess();
            var btnBrowse = Btn("📁  Buscar el .exe del juego", 224, 184, 208, 34, AccentBlue);
            Tip(btnBrowse, "Busca el archivo .exe del juego en tu disco, por si no esta abierto ahora mismo.");
            btnBrowse.Click += (_, _) => BrowseGameExe();
            card2.Controls.AddRange(new Control[] { btnPick, btnBrowse });
            var btnPrio = Btn("Dar prioridad Alta ahora", 12, 228, 240, 34, AccentBlue);
            Tip(btnPrio, "Le dice a Windows que atienda primero a tu juego antes que a las demas apps. Si el juego tiene anti-cheat, puede que no lo permita, y te explicare por que abajo.");
            btnPrio.Click += (_, _) => { GameModeService.PrioritizeGame(txtGameExe.Text.Trim(), Log); ShowPriorityInfo(); SetStatus("Prioridad: revisa el mensaje de abajo."); };
            card2.Controls.Add(btnPrio);
            card2.Controls.Add(Hint("Abre el juego primero y luego pulsa Elegir mi juego abierto.", 12, 268, 420));
            p.Controls.Add(card2);

            // Caja informativa sobre la prioridad / anti-cheat, siempre visible
            lblPriorityInfo = new Label
            {
                Text = "ℹ️  Sobre la prioridad: algunos juegos con anti-cheat (Battlefield 6, Valorant, Fortnite) no permiten que ningun programa externo les cambie la prioridad. Si te sale ese aviso, no es un error tuyo ni de GameBoost: el resto del Modo Juego sigue funcionando.",
                Location = new Point(16, 372), Size = new Size(940, 44),
                ForeColor = Color.FromArgb(219, 234, 254), BackColor = Theme.InfoBg,
                Font = new Font("Segoe UI", 9f), Padding = new Padding(8, 6, 8, 6),
            };
            p.Controls.Add(lblPriorityInfo);

            btnGameOn = Btn("🎮  ACTIVAR Modo Juego", 16, 428, 300, 48, Accent);
            Tip(btnGameOn, "Aplica todo lo marcado arriba de una vez: cierra apps, Alto rendimiento, pausa Windows Update y prioriza tu juego.");
            btnGameOn.Click += (_, _) => SetGameMode(true);
            btnGameOff = Btn("⏸  DESACTIVAR Modo Juego", 332, 428, 300, 48, Danger);
            Tip(btnGameOff, "Devuelve todo a como estaba: plan Equilibrado, Windows Update reanudado y bloqueo de teclas apagado.");
            btnGameOff.Enabled = false;
            btnGameOff.Click += (_, _) => SetGameMode(false);
            p.Controls.AddRange(new Control[] { btnGameOn, btnGameOff });

            lblGameStatus = Lbl("Modo Juego: APAGADO", 16, 484, 940, true, TextMuted);
            p.Controls.Add(lblGameStatus);
        }

        private void PickGameProcess()
        {
            using var dlg = new ProcessPickerForm();
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedExe))
            {
                txtGameExe.Text = dlg.SelectedExe;
                Log($"Juego elegido: {dlg.SelectedExe} (PID {dlg.SelectedPid})");
                SetStatus("Juego elegido: " + dlg.SelectedExe);
            }
        }

        private void BrowseGameExe()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Elegir el .exe del juego",
                Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                txtGameExe.Text = System.IO.Path.GetFileName(dlg.FileName);
                Log($"Juego elegido por archivo: {txtGameExe.Text}  (ruta: {dlg.FileName})");
                SetStatus("Juego elegido: " + txtGameExe.Text);
            }
        }

        private void ShowPriorityInfo()
        {
            if (lblPriorityInfo == null) return;
            string msg = GameModeService.LastPriorityMessage;
            if (string.IsNullOrWhiteSpace(msg)) return;
            // Mensaje descriptivo, no un error seco: explica que paso y por que.
            lblPriorityInfo.Text = "ℹ️  " + msg;
            lblPriorityInfo.BackColor = GameModeService.LastPriorityBlockedByAntiCheat
                ? Color.FromArgb(69, 26, 3)   // ambar oscuro, aviso
                : Theme.InfoBg;
            lblPriorityInfo.ForeColor = GameModeService.LastPriorityBlockedByAntiCheat
                ? Color.FromArgb(254, 215, 170)
                : Color.FromArgb(219, 234, 254);
        }

        private void SetGameMode(bool on)
        {
            if (on)
            {
                Log("=== ACTIVANDO Modo Juego ===");
                if (chkCloseApps.Checked)
                {
                    var list = new List<string>();
                    if (chkChrome.Checked) list.Add("chrome.exe");
                    if (chkEdge.Checked) list.Add("msedge.exe");
                    if (chkFirefox.Checked) list.Add("firefox.exe");
                    if (chkOneDrive.Checked) list.Add("OneDrive.exe");
                    if (chkSpotify.Checked) list.Add("Spotify.exe");
                    if (chkDiscord.Checked) list.Add("Discord.exe");
                    if (list.Count > 0) GameModeService.CloseProcesses(list, Log);
                }
                if (chkPower.Checked) GameModeService.SetPowerPlan(true, Log);
                if (chkUpdate.Checked) GameModeService.SetWindowsUpdate(true, Log);
                if (!string.IsNullOrWhiteSpace(txtGameExe.Text)) { GameModeService.PrioritizeGame(txtGameExe.Text.Trim(), Log); ShowPriorityInfo(); }
                if (chkAutoBlock != null && chkAutoBlock.Checked) StartKeyBlockFromOptions();
                lblGameStatus.Text = "Modo Juego: ACTIVADO  ✓  —  juega tranquilo, al terminar pulsa DESACTIVAR.";
                lblGameStatus.ForeColor = Accent;
                btnGameOn.Enabled = false; btnGameOff.Enabled = true;
                SetStatus("Modo Juego activado.");
                Log("Modo Juego activado.");
            }
            else
            {
                Log("=== DESACTIVANDO Modo Juego ===");
                if (chkPower.Checked) GameModeService.SetPowerPlan(false, Log);
                if (chkUpdate.Checked) GameModeService.SetWindowsUpdate(false, Log);
                if (KeyBlocker.IsActive) { KeyBlocker.Stop(Log); UpdateKeyBlockStatus(); }
                lblGameStatus.Text = "Modo Juego: APAGADO";
                lblGameStatus.ForeColor = TextMuted;
                btnGameOn.Enabled = true; btnGameOff.Enabled = false;
                SetStatus("Modo Juego desactivado, todo restaurado.");
                Log("Modo Juego desactivado.");
            }
        }

        // --- Tab 4: Optimizacion y DNS ---
        private Label lblAdapter = null!, lblCurrentDns = null!;
        private RadioButton rbCloudflare = null!, rbGoogle = null!, rbAuto = null!;

        private void BuildTabOptimizacion()
        {
            var p = tabOpt;
            p.Controls.Add(Lbl("Red: ajustes de TCP/IP y DNS para que tus paquetes lleguen antes.", 16, 12, 920, true));
            var cardInfo = Card(16, 44, 940, 92);
            lblAdapter = Lbl("Adaptador: detectando...", 12, 10, 900, true);
            lblCurrentDns = Lbl("DNS actual: detectando...", 12, 36, 900);
            var lblWifi = Lbl("Si dice Wi-Fi, el mejor consejo sigue siendo: conecta el cable Ethernet.", 12, 62, 900, false, Warn);
            cardInfo.Controls.AddRange(new Control[] { lblAdapter, lblCurrentDns, lblWifi });
            p.Controls.Add(cardInfo);

            var cardTcp = Card(16, 150, 460, 260);
            cardTcp.Controls.Add(Lbl("Ajustes de red (TCP/IP)", 12, 10, 400, true));
            cardTcp.Controls.Add(Lbl("Hacemos que los paquetes de tu juego salgan sin esperar (autotuning, RSS, ECN y sin Nagle). Si algo se siente raro, se deshace con el boton rojo de abajo.", 12, 36, 430, false, TextMuted, 54));
            var btnApplyTcp = Btn("Aplicar ajustes de red", 12, 110, 260, 40, Accent);
            Tip(btnApplyTcp, "Ajusta Windows para que los paquetes del juego salgan sin esperar. Se puede deshacer con Restaurar.");
            btnApplyTcp.Click += (_, _) => { Log("=== Aplicando optimizacion TCP/IP ==="); NetworkOptimizer.Apply(Log); SetStatus("Optimizacion TCP/IP aplicada. Revisa el registro."); RefreshSystemInfo(); };
            var btnRestoreTcp = Btn("Restaurar valores de Windows", 12, 162, 260, 40, Danger);
            Tip(btnRestoreTcp, "Devuelve todos los ajustes TCP/IP a como venian de fabrica en Windows. Usalo si algo se siente raro.");
            btnRestoreTcp.Click += (_, _) => { Log("=== Restaurando valores de Windows ==="); NetworkOptimizer.Restore(Log); SetStatus("Valores restaurados."); RefreshSystemInfo(); };
            cardTcp.Controls.AddRange(new Control[] { btnApplyTcp, btnRestoreTcp });
            cardTcp.Controls.Add(Lbl("Se guarda un respaldo en %AppData%\\GameBoost\\backup.json", 12, 214, 430, false, TextMuted));
            p.Controls.Add(cardTcp);

            var cardDns = Card(492, 150, 464, 260);
            cardDns.Controls.Add(Lbl("Cambiar el DNS (conectar mas rapido)", 12, 10, 400, true));
            cardDns.Controls.Add(Lbl("El DNS es como la guia de telefonos de internet: uno rapido te conecta antes, pero no baja el ping dentro del juego. Aun asi, uno lento si puede retrasar el inicio.", 12, 36, 430, false, TextMuted, 54));
            rbCloudflare = new RadioButton { Text = "Cloudflare  1.1.1.1  /  1.0.0.1  (recomendado)", Location = new Point(12, 98), Size = new Size(430, 24), ForeColor = TextLight, Checked = true };
            rbGoogle = new RadioButton { Text = "Google  8.8.8.8  /  8.8.4.4", Location = new Point(12, 126), Size = new Size(430, 24), ForeColor = TextLight };
            rbAuto = new RadioButton { Text = "Automatico (DHCP, el de tu ISP)", Location = new Point(12, 152), Size = new Size(430, 24), ForeColor = TextLight };
            cardDns.Controls.AddRange(new Control[] { rbCloudflare, rbGoogle, rbAuto });
            var btnDns = Btn("Cambiar DNS", 12, 190, 200, 38, AccentBlue);
            Tip(btnDns, "Cambia el DNS al que elegiste arriba (Cloudflare o Google). Solo ayuda a conectar mas rapido, no baja el ping dentro del juego.");
            btnDns.Click += (_, _) => ApplyDnsChoice();
            var btnFlush = Btn("Limpiar cache DNS", 224, 190, 200, 38, AccentBlue);
            Tip(btnFlush, "Borra las direcciones guardadas para que Windows vuelva a buscarlas frescas. No borra nada de tus archivos.");
            btnFlush.Click += (_, _) => { var r = ProcessRunner.RunCmd("ipconfig /flushdns"); Log("ipconfig /flushdns: " + r.Output.Split('\n').FirstOrDefault()?.Trim()); SetStatus("Cache DNS limpiada."); };
            cardDns.Controls.AddRange(new Control[] { btnDns, btnFlush });
            p.Controls.Add(cardDns);

            p.Controls.Add(Lbl("Despues de aplicar, mide de nuevo en Medidor con Marcar ANTES / DESPUES. Si en 2 pruebas no mejora nada, restaura: en tu caso el limite es tu internet (ISP) o la distancia al servidor, no tu PC.", 16, 426, 940, false, TextMuted, 40));
            var btnRefresh = Btn("Volver a leer el adaptador", 16, 458, 280, 36, AccentBlue);
            btnRefresh.Click += (_, _) => RefreshSystemInfo();
            p.Controls.Add(btnRefresh);
        }

        private void ApplyDnsChoice()
        {
            var adapter = SystemInfoHelper.GetActiveAdapter();
            if (adapter == null) { MessageBox.Show("No encontre un adaptador activo (Ethernet o Wi-Fi).", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Log($"=== Cambiando DNS en '{adapter.Name}' ===");
            string result;
            if (rbAuto.Checked) result = DnsHelper.RestoreDhcp(adapter.Name, Log);
            else if (rbGoogle.Checked) result = DnsHelper.SetDns(adapter.Name, "8.8.8.8", "8.8.4.4", Log);
            else result = DnsHelper.SetDns(adapter.Name, "1.1.1.1", "1.0.0.1", Log);
            Log("Resultado DNS: " + result);
            SetStatus("DNS actualizado: " + result);
            RefreshSystemInfo();
        }

        private void RefreshSystemInfo()
        {
            var a = SystemInfoHelper.GetActiveAdapter();
            if (a == null)
            {
                if (lblAdapter != null) lblAdapter.Text = "Adaptador: no se encontro Ethernet o Wi-Fi activo.";
                if (lblCurrentDns != null) lblCurrentDns.Text = "DNS actual: -";
                return;
            }
            if (lblAdapter != null)
                lblAdapter.Text = $"Adaptador: {a.Name}  •  {a.Type}  •  {a.Speed}  •  IP {a.Ip}  •  Puerta de enlace {a.Gateway}";
            if (lblCurrentDns != null)
                lblCurrentDns.Text = $"DNS actual: {a.Dns}" + (a.IsWifi ? "   ⚠ Estas por Wi-Fi: usa cable si puedes." : "   ✓ Por cable, bien.");
        }
    }
}
