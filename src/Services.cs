using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameBoost
{
    // ---------- Utilidad para ejecutar comandos de Windows ----------
    public static class ProcessRunner
    {
        public static (int ExitCode, string Output) Run(string file, string args, int timeoutMs = 15000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return (-1, "No se pudo iniciar " + file);
                string outText = p.StandardOutput.ReadToEnd();
                string errText = p.StandardError.ReadToEnd();
                bool exited = p.WaitForExit(timeoutMs);
                if (!exited) { try { p.Kill(); } catch { } return (-1, "Tiempo agotado: " + file + " " + args); }
                string all = (outText + "\n" + errText).Trim();
                return (p.ExitCode, all);
            }
            catch (Exception ex) { return (-1, ex.Message); }
        }

        public static (int ExitCode, string Output) RunNetsh(string args) => Run("netsh", args);
        public static (int ExitCode, string Output) RunCmd(string args) => Run("cmd.exe", "/c " + args);
    }

    // ---------- Ping ----------
    public class PingSample
    {
        public long? Ms { get; set; }
        public bool Ok => Ms.HasValue;
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public class PingStats
    {
        public int Total, OkCount, Lost;
        public double LossPct => Total == 0 ? 0 : Lost * 100.0 / Total;
        public long Min, Max;
        public double Avg, Jitter;
        public string Verdict
        {
            get
            {
                if (Total == 0) return "-";
                if (LossPct >= 10) return "Malo: mucha perdida";
                if (Avg < 30 && Jitter < 10) return "Excelente";
                if (Avg < 60 && Jitter < 20) return "Bueno";
                if (Avg < 100) return "Regular";
                return "Malo: ping alto";
            }
        }
    }

    public static class PingService
    {
        public static async Task<PingSample> PingOnceAsync(string host, int timeoutMs = 1000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                if (reply.Status == IPStatus.Success)
                    return new PingSample { Ms = reply.RoundtripTime };
                return new PingSample { Ms = null };
            }
            catch { return new PingSample { Ms = null }; }
        }

        public static async Task<PingStats> PingManyAsync(string host, int count = 4, int timeoutMs = 1000)
        {
            var samples = new List<PingSample>();
            for (int i = 0; i < count; i++)
            {
                samples.Add(await PingOnceAsync(host, timeoutMs));
                await Task.Delay(120);
            }
            return Compute(samples);
        }

        public static PingStats Compute(List<PingSample> samples)
        {
            var ok = samples.Where(s => s.Ok).Select(s => s.Ms!.Value).ToList();
            var st = new PingStats { Total = samples.Count, OkCount = ok.Count, Lost = samples.Count - ok.Count };
            if (ok.Count == 0) return st;
            st.Min = ok.Min(); st.Max = ok.Max(); st.Avg = ok.Average();
            if (ok.Count >= 2)
            {
                double sum = 0;
                for (int i = 1; i < ok.Count; i++) sum += Math.Abs(ok[i] - ok[i - 1]);
                st.Jitter = sum / (ok.Count - 1);
            }
            return st;
        }

        public static PingStats ComputeFromMs(List<long> okMs, int total)
        {
            var samples = okMs.Select(m => new PingSample { Ms = m }).ToList();
            while (samples.Count < total) samples.Add(new PingSample { Ms = null });
            return Compute(samples);
        }
    }

    // ---------- Respaldo ----------
    public class BackupData
    {
        public DateTime Created { get; set; } = DateTime.Now;
        public string AdapterName { get; set; } = "";
        public string DnsPrimary { get; set; } = "";
        public string DnsSecondary { get; set; } = "";
        public Dictionary<string, string> RegistryValues { get; set; } = new();
    }

    public static class BackupStore
    {
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameBoost");
        public static string Path_ => System.IO.Path.Combine(Folder, "backup.json");
        public static void Save(BackupData b)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path_, JsonSerializer.Serialize(b, new JsonSerializerOptions { WriteIndented = true }));
        }
        public static BackupData? Load()
        {
            try
            {
                if (!File.Exists(Path_)) return null;
                return JsonSerializer.Deserialize<BackupData>(File.ReadAllText(Path_));
            }
            catch { return null; }
        }
    }

    // ---------- Info del sistema / adaptador ----------
    public class AdapterInfo
    {
        public string Name = "", Description = "", Type = "", Speed = "", Ip = "", Gateway = "", Dns = "";
        public bool IsWifi;
    }

    public static class SystemInfoHelper
    {
        public static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static AdapterInfo? GetActiveAdapter()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) continue;
                var props = ni.GetIPProperties();
                var gw = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                if (gw == null) continue;
                var ip = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                var dns = string.Join(", ", props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString()));
                return new AdapterInfo
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Ethernet (cable)",
                    IsWifi = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                    Speed = (ni.Speed / 1_000_000) + " Mbps",
                    Ip = ip?.Address.ToString() ?? "-",
                    Gateway = gw.Address.ToString(),
                    Dns = string.IsNullOrWhiteSpace(dns) ? "Automatico (DHCP)" : dns
                };
            }
            return null;
        }
    }

    // ---------- DNS ----------
    public static class DnsHelper
    {
        public static string SetDns(string adapterName, string primary, string secondary, Action<string> log)
        {
            var r1 = ProcessRunner.RunNetsh($"interface ipv4 set dnsservers name=\"{adapterName}\" source=static address={primary} register=primary validate=no");
            log($"DNS primario {primary}: salida {r1.ExitCode} - {FirstLine(r1.Output)}");
            if (!string.IsNullOrWhiteSpace(secondary))
            {
                var r2 = ProcessRunner.RunNetsh($"interface ipv4 add dnsservers name=\"{adapterName}\" address={secondary} index=2 validate=no");
                log($"DNS secundario {secondary}: salida {r2.ExitCode} - {FirstLine(r2.Output)}");
            }
            ProcessRunner.RunCmd("ipconfig /flushdns");
            return r1.ExitCode == 0 ? "OK" : "No se pudo cambiar el DNS (netsh devolvio " + r1.ExitCode + "). Prueba ejecutando GameBoost como administrador.";
        }

        public static string RestoreDhcp(string adapterName, Action<string> log)
        {
            var r = ProcessRunner.RunNetsh($"interface ipv4 set dnsservers name=\"{adapterName}\" source=dhcp");
            log($"DNS a automatico (DHCP): salida {r.ExitCode} - {FirstLine(r.Output)}");
            ProcessRunner.RunCmd("ipconfig /flushdns");
            return r.ExitCode == 0 ? "OK" : "No se pudo volver al DNS automatico (netsh devolvio " + r.ExitCode + "). Prueba ejecutando GameBoost como administrador.";
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(sin salida)";
            var line = s.Split('\n').FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
            return line.Trim().Length > 120 ? line.Trim().Substring(0, 120) : line.Trim();
        }
    }

    // ---------- Optimizacion TCP/IP + Registro ----------
    public static class NetworkOptimizer
    {
        public static void Apply(Action<string> log)
        {
            // 1) Respaldo de DNS actual antes de tocar nada
            var adapter = SystemInfoHelper.GetActiveAdapter();
            var backup = BackupStore.Load() ?? new BackupData();
            if (adapter != null)
            {
                backup.AdapterName = adapter.Name;
                var dnsParts = adapter.Dns.Split(',', StringSplitOptions.TrimEntries);
                if (dnsParts.Length >= 1 && !adapter.Dns.StartsWith("Automatico")) backup.DnsPrimary = dnsParts[0];
                if (dnsParts.Length >= 2) backup.DnsSecondary = dnsParts[1];
            }
            BackupStore.Save(backup);
            log("Respaldo guardado en %AppData%\\GameBoost\\backup.json");

            // 2) netsh - solo ajustes seguros y reversibles
            var cmds = new (string Args, string Label)[]
            {
                ("int tcp set global autotuninglevel=normal", "Autotuning TCP = normal"),
                ("int tcp set global rss=enabled", "RSS = activado"),
                ("int tcp set global ecncapability=disabled", "ECN = desactivado (evita problemas con algunos routers)"),
                ("int tcp set global timestamps=disabled", "Timestamps TCP = desactivado"),
                ("int tcp set global initialrto=2000", "RTO inicial = 2000 ms"),
            };
            foreach (var (args, label) in cmds)
            {
                var r = ProcessRunner.RunNetsh(args);
                log($"{label}: {(r.ExitCode == 0 ? "OK" : "no se aplico (" + r.ExitCode + ")")}");
            }

            // 3) Registro: desactivar Nagle (TcpNoDelay) y ACK retardado, por interfaz
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true);
                if (baseKey != null)
                {
                    foreach (var name in baseKey.GetSubKeyNames())
                    {
                        using var k = baseKey.OpenSubKey(name, true);
                        if (k == null) continue;
                        SaveReg(backup, k.Name + "\\TcpNoDelay", k.GetValue("TcpNoDelay"));
                        SaveReg(backup, k.Name + "\\TcpAckFrequency", k.GetValue("TcpAckFrequency"));
                        k.SetValue("TcpNoDelay", 1, RegistryValueKind.DWord);
                        k.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        k.SetValue("TcpDelAckTicks", 0, RegistryValueKind.DWord);
                    }
                    log("Registro: TcpNoDelay=1 y TcpAckFrequency=1 en interfaces (Nagle desactivado).");
                }
            }
            catch (Exception ex) { log("Registro TCP: no se pudo escribir (" + ex.Message + "). Ejecuta como administrador."); }

            // 4) Multimedia / throttling
            try
            {
                using var mm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
                if (mm != null)
                {
                    SaveReg(backup, "SystemProfile\\NetworkThrottlingIndex", mm.GetValue("NetworkThrottlingIndex"));
                    SaveReg(backup, "SystemProfile\\SystemResponsiveness", mm.GetValue("SystemResponsiveness"));
                    mm.SetValue("NetworkThrottlingIndex", unchecked((int)0xffffffff), RegistryValueKind.DWord);
                    mm.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                    log("Registro: NetworkThrottlingIndex y SystemResponsiveness ajustados para juegos.");
                }
                BackupStore.Save(backup);
            }
            catch (Exception ex) { log("Registro multimedia: " + ex.Message); }

            ProcessRunner.RunCmd("ipconfig /flushdns");
            log("DNS cache limpiada (ipconfig /flushdns).");
            log("Listo. Algunos cambios se notan al instante; otros, tras reiniciar el juego o la PC.");
        }

        public static void Restore(Action<string> log)
        {
            var cmds = new (string Args, string Label)[]
            {
                ("int tcp set global autotuninglevel=normal", "Autotuning TCP = normal"),
                ("int tcp set global rss=enabled", "RSS = activado"),
                ("int tcp set global ecncapability=disabled", "ECN = desactivado"),
                ("int tcp set global timestamps=enabled", "Timestamps TCP = activado (valor de Windows)"),
            };
            foreach (var (args, label) in cmds)
            {
                var r = ProcessRunner.RunNetsh(args);
                log($"{label}: {(r.ExitCode == 0 ? "OK" : "no se aplico (" + r.ExitCode + ")")}");
            }
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true);
                if (baseKey != null)
                {
                    foreach (var name in baseKey.GetSubKeyNames())
                    {
                        using var k = baseKey.OpenSubKey(name, true);
                        if (k == null) continue;
                        k.DeleteValue("TcpNoDelay", false);
                        k.DeleteValue("TcpAckFrequency", false);
                        k.DeleteValue("TcpDelAckTicks", false);
                    }
                    log("Registro: valores TcpNoDelay / TcpAckFrequency eliminados (vuelven al valor de Windows).");
                }
                using var mm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
                if (mm != null)
                {
                    mm.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                    mm.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                    log("Registro: NetworkThrottlingIndex=10 y SystemResponsiveness=20 (valores de Windows).");
                }
            }
            catch (Exception ex) { log("Restaurar registro: " + ex.Message); }

            var backup = BackupStore.Load();
            if (backup != null && !string.IsNullOrWhiteSpace(backup.AdapterName))
            {
                log("DNS guardado en respaldo: " + (string.IsNullOrWhiteSpace(backup.DnsPrimary) ? "automatico" : backup.DnsPrimary));
            }
            ProcessRunner.RunCmd("ipconfig /flushdns");
            log("Restauracion terminada. Reinicia el juego para que todo tome efecto.");
        }

        private static void SaveReg(BackupData b, string key, object? value)
        {
            if (value == null) return;
            if (!b.RegistryValues.ContainsKey(key)) b.RegistryValues[key] = value.ToString() ?? "";
        }
    }

    // ---------- Modo Juego ----------
    public static class GameModeService
    {
        public static int CloseProcesses(IEnumerable<string> exeNames, Action<string> log)
        {
            int closed = 0;
            foreach (var exe in exeNames)
            {
                string name = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(); closed++; log($"Cerrado: {p.ProcessName} (PID {p.Id})"); } catch (Exception ex) { log($"No se pudo cerrar {name}: {ex.Message}"); }
                    }
                }
                catch (Exception ex) { log($"Buscar {name}: {ex.Message}"); }
            }
            if (closed == 0) log("No habia procesos de esa lista en ejecucion.");
            return closed;
        }

        // Cierre de emergencia: mata el juego elegido (por si se crashea y no responde).
        // Devuelve (cerrados, mensaje). No toca ningun otro proceso.
        public static (int Closed, string Message) KillGame(string exeName, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(exeName))
                return (0, "No hay juego elegido en Modo Juego. Elige tu juego primero (Elegir proceso o Examinar .exe) y luego usa el cierre de emergencia.");
            string name = System.IO.Path.GetFileNameWithoutExtension(exeName.Trim());
            int closed = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(entireProcessTree: true); closed++; } catch { try { p.Kill(); closed++; } catch (Exception ex) { log($"No se pudo cerrar {name}: {ex.Message}"); } }
                }
            }
            catch (Exception ex) { return (0, "No se pudo buscar el juego: " + ex.Message); }
            string msg = closed > 0
                ? $"Juego cerrado por emergencia: {name} ({closed} proceso(s)). Si seguia congelado, ya no deberia bloquear la pantalla."
                : $"El juego '{name}' no estaba en ejecucion. Nada que cerrar.";
            log(msg);
            return (closed, msg);
        }

        public static void SetPowerPlan(bool high, Action<string> log)
        {
            string guid = high ? "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" : "381b4222-f694-41f0-9685-ff5bb260df2e";
            var r = ProcessRunner.Run("powercfg", "/s " + guid);
            log(high ? $"Plan de energia: Alto rendimiento ({(r.ExitCode == 0 ? "OK" : "codigo " + r.ExitCode)})"
                     : $"Plan de energia: Equilibrado ({(r.ExitCode == 0 ? "OK" : "codigo " + r.ExitCode)})");
        }

        public static void SetWindowsUpdate(bool stop, Action<string> log)
        {
            var r = ProcessRunner.Run("sc", stop ? "stop wuauserv" : "start wuauserv");
            log(stop ? $"Windows Update detenido (sc stop wuauserv, codigo {r.ExitCode}). Se reanuda al desactivar el Modo Juego o al reiniciar."
                     : $"Windows Update reanudado (sc start wuauserv, codigo {r.ExitCode}).");
        }

        /// <summary>Ultimo mensaje descriptivo sobre la prioridad, para mostrarlo en la interfaz.</summary>
        public static string LastPriorityMessage { get; private set; } = "";
        public static bool LastPriorityBlockedByAntiCheat { get; private set; }

        private static readonly string[] ProtectedGames = { "bf6", "valorant", "fortnite", "apex", "rust", "eldenring", "overwatch", "callofduty", "warzone" };

        public static bool IsKnownProtectedGame(string exeName)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(exeName.Trim()).ToLowerInvariant();
            foreach (var g in ProtectedGames) if (n.Contains(g)) return true;
            return false;
        }

        public static string AntiCheatExplanation(string file)
        {
            return $"No se pudo cambiar la prioridad de {file}: esta protegido por su anti-cheat " +
                "(en Battlefield 6 es EA Javelin, en Valorant es Vanguard, en Fortnite es Easy Anti-Cheat). " +
                "El anti-cheat no deja que ningun programa externo toque el proceso del juego, aunque GameBoost corra como administrador. " +
                "No es un error tuyo ni de GameBoost, y no se debe forzar: intentar saltarlo puede provocar un baneo de la cuenta. " +
                "El resto del Modo Juego (cerrar apps, Alto rendimiento, pausar Windows Update) sigue funcionando y es lo que mas ayuda.";
        }

        public static void PrioritizeGame(string exeName, Action<string> log)
        {
            LastPriorityBlockedByAntiCheat = false;
            if (string.IsNullOrWhiteSpace(exeName))
            {
                LastPriorityMessage = "No elegiste el .exe del juego. Abre el juego y pulsa 'Elegir proceso...' para seleccionarlo de la lista.";
                log(LastPriorityMessage);
                return;
            }
            // Acepta ruta completa ("C:\\Juegos\\...\\juego.exe"), nombre con .exe o sin el.
            string file = System.IO.Path.GetFileName(exeName.Trim());
            string name = file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
            var procs = Process.GetProcessesByName(name);
            if (procs.Length == 0)
            {
                LastPriorityMessage = $"El juego '{file}' no esta en ejecucion ahora. Abre el juego primero y vuelve a pulsar Priorizar (o elijelo con 'Elegir proceso...').";
                log(LastPriorityMessage);
                return;
            }
            foreach (var p in procs)
            {
                // 1) Intentar prioridad Alta, que es lo que pide el usuario.
                try
                {
                    p.PriorityClass = ProcessPriorityClass.High;
                    LastPriorityMessage = $"Prioridad Alta puesta a {p.ProcessName} (PID {p.Id}). Listo, el juego tiene preferencia sobre las demas apps.";
                    log(LastPriorityMessage);
                    continue;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception)
                {
                    // 2) Si el anti-cheat lo bloquea, probar un nivel mas suave.
                    try
                    {
                        p.PriorityClass = ProcessPriorityClass.AboveNormal;
                        LastPriorityMessage = $"Prioridad Alta bloqueada, pero se puso 'Por encima de lo normal' a {p.ProcessName}. Es casi lo mismo en la practica.";
                        log(LastPriorityMessage);
                        continue;
                    }
                    catch { /* cae al mensaje descriptivo de abajo */ }

                    if (IsKnownProtectedGame(file))
                    {
                        LastPriorityBlockedByAntiCheat = true;
                        LastPriorityMessage = AntiCheatExplanation(file);
                    }
                    else
                    {
                        LastPriorityBlockedByAntiCheat = false;
                        LastPriorityMessage = $"No se pudo cambiar la prioridad de {file}: Windows denego el acceso. Puede ser un proceso del sistema o con proteccion propia. Comprueba que GameBoost corre como administrador (arriba debe decir 'Ejecutando como administrador') y vuelve a intentarlo. El resto del Modo Juego sigue funcionando.";
                    }
                    log(LastPriorityMessage);
                }
                catch (Exception ex)
                {
                    LastPriorityMessage = $"No se pudo priorizar {p.ProcessName}: {ex.Message}";
                    log(LastPriorityMessage);
                }
            }
        }
    }

    // ---------- Enlaces oficiales (donaciones / Nous Logic) ----------
    public static class AppLinks
    {
        public const string PayPal = "https://www.paypal.com/paypalme/engelarm";
        public const string Coffee = "https://buymeacoffee.com/engelarm";
        public const string Website = "https://nouslogic.xyz";

        public static bool OpenUrl(string url, Action<string>? log = null)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                log?.Invoke("Abriendo en el navegador: " + url);
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("No se pudo abrir el navegador (" + ex.Message + "). Copia el enlace: " + url);
                return false;
            }
        }
    }
}
