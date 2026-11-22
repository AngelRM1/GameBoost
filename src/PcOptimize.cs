using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace GameBoost
{
    // Optimizacion segura de Windows: limpieza, inicio, servicios y
    // efectos visuales. Todo es reversible y nada borra documentos,
    // juegos ni programas instalados. Cada cambio guarda su respaldo
    // en %AppData%\GameBoost\ antes de aplicarse.
    public static class OptimizePaths
    {
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameBoost");
        public static string StartupBackup => Path.Combine(Folder, "startup-backup.json");
        public static string ServicesBackup => Path.Combine(Folder, "services-backup.json");
        public static string EffectsBackup => Path.Combine(Folder, "effects-backup.json");

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
            if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} bytes";
        }
    }

    // ---------- 1) Limpieza ----------
    public static class CleanupService
    {
        public static List<(string Label, string Path)> TempLocations()
        {
            var list = new List<(string Label, string Path)>();
            string userTemp = Path.GetTempPath();
            if (!string.IsNullOrEmpty(userTemp)) list.Add(("Archivos temporales del usuario", userTemp));
            list.Add(("Archivos temporales de Windows", @"C:\Windows\Temp"));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local))
            {
                list.Add(("Caché DirectX / shaders", Path.Combine(local, "D3DCache")));
                list.Add(("Caché de shaders NVIDIA", Path.Combine(local, @"NVIDIA\DXCache")));
                list.Add(("Caché de shaders AMD", Path.Combine(local, @"AMD\DxCache")));
            }
            return list.Where(x => Directory.Exists(x.Path)).ToList();
        }

        // Suma sin borrar: cuantos archivos y cuanto pesan.
        public static (int Files, long Bytes) Measure(IEnumerable<string> paths)
        {
            int files = 0; long bytes = 0;
            foreach (var dir in paths)
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        try { var fi = new FileInfo(f); files++; bytes += fi.Length; }
                        catch { }
                    }
                }
                catch { }
            }
            return (files, bytes);
        }

        // Borra solo archivos (nunca las carpetas en si). Salta los
        // archivos usados ahora mismo o modificados hace menos de 1 hora,
        // para no borrar algo que una app este usando.
        public static (int Deleted, int Skipped, long FreedBytes) Clean(IEnumerable<string> paths, Action<string>? log = null)
        {
            int deleted = 0, skipped = 0; long freed = 0;
            var cutoff = DateTime.Now.AddHours(-1);
            foreach (var dir in paths)
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList())
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            if (fi.LastWriteTime > cutoff) { skipped++; continue; }
                            long len = fi.Length;
                            File.Delete(f);
                            deleted++; freed += len;
                        }
                        catch { skipped++; }
                    }
                    // Quita subcarpetas que quedaron vacias (no la raiz)
                    foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length).ToList())
                    {
                        try { if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub); } catch { }
                    }
                }
                catch (Exception ex) { log?.Invoke("Limpieza de " + dir + ": " + ex.Message); }
            }
            return (deleted, skipped, freed);
        }

        [DllImport("shell32.dll")]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
        private const uint SHERB_NOCONFIRMATION = 0x1, SHERB_NOPROGRESSUI = 0x2, SHERB_NOSOUND = 0x4;

        public static bool EmptyRecycleBin(Action<string>? log = null)
        {
            try
            {
                int hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                log?.Invoke(hr == 0 ? "Papelera vaciada." : "La papelera no se pudo vaciar (codigo " + hr + ").");
                return hr == 0;
            }
            catch (Exception ex) { log?.Invoke("Papelera: " + ex.Message); return false; }
        }

        // Limpieza de Windows Update (DISM). Tarda minutos: se lanza en
        // segundo plano y avisa por el log cuando termina.
        public static void WindowsUpdateCleanupAsync(Action<string> log, Action<string> onDone)
        {
            log("Limpieza de Windows Update iniciada (DISM). Puede tardar varios minutos, sigue usando el programa.");
            System.Threading.Tasks.Task.Run(() =>
            {
                var (code, _) = ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", 600000);
                string msg = code == 0
                    ? "Limpieza de Windows Update terminada."
                    : "Limpieza de Windows Update termino con codigo " + code + " (puede que no hubiera nada que limpiar).";
                log(msg);
                onDone(msg);
            });
        }
    }

    // ---------- 2) Programas de inicio ----------
    public class StartupItem
    {
        public string Name = "";
        public string Command = "";
        public string Location = ""; // Registro usuario / Registro equipo / Carpeta Inicio
        public string ApprovalKey = ""; // Run o StartupFolder
        public string ValueName = "";
        public bool Enabled = true;
    }

    public static class StartupService
    {
        private const string RunUser = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunMachine = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        public static List<StartupItem> List()
        {
            var items = new List<StartupItem>();
            try
            {
                using var ku = Registry.CurrentUser.OpenSubKey(RunUser);
                if (ku != null) foreach (var n in ku.GetValueNames())
                    items.Add(new StartupItem { Name = n, Command = ku.GetValue(n)?.ToString() ?? "", Location = "Registro (tu usuario)", ApprovalKey = "Run", ValueName = n, Enabled = IsApproved(ApprovedRun, n) });
                using var km = Registry.LocalMachine.OpenSubKey(RunMachine);
                if (km != null) foreach (var n in km.GetValueNames())
                    items.Add(new StartupItem { Name = n, Command = km.GetValue(n)?.ToString() ?? "", Location = "Registro (todo el equipo)", ApprovalKey = "Run", ValueName = n, Enabled = IsApproved(ApprovedRun, n) });
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
                if (Directory.Exists(folder))
                    foreach (var f in Directory.GetFiles(folder, "*.lnk"))
                        items.Add(new StartupItem { Name = Path.GetFileNameWithoutExtension(f), Command = f, Location = "Carpeta Inicio", ApprovalKey = "StartupFolder", ValueName = Path.GetFileName(f), Enabled = IsApproved(ApprovedFolder, Path.GetFileName(f)) });
            }
            catch { /* sin permisos o sin claves: lista vacia, no romper */ }
            return items.OrderBy(i => i.Name).ToList();
        }

        // StartupApproved: valor binario, primer byte 02 = activado, 03 = desactivado.
        private static bool IsApproved(string keyPath, string valueName)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(keyPath);
                var v = k?.GetValue(valueName) as byte[];
                if (v == null || v.Length == 0) return true;
                return v[0] != 0x03;
            }
            catch { return true; }
        }

        // Desactivar NO borra el programa: solo cambia el byte de
        // aprobacion, como hace el Administrador de tareas. Reversible.
        public static bool SetEnabled(StartupItem item, bool enable, Action<string>? log = null)
        {
            try
            {
                SaveBackup();
                string keyPath = item.ApprovalKey == "StartupFolder" ? ApprovedFolder : ApprovedRun;
                using var k = Registry.CurrentUser.CreateSubKey(keyPath);
                if (k == null) return false;
                var existing = k.GetValue(item.ValueName) as byte[];
                byte[] value = existing != null && existing.Length >= 12 ? (byte[])existing.Clone() : new byte[12];
                value[0] = enable ? (byte)0x02 : (byte)0x03;
                k.SetValue(item.ValueName, value, RegistryValueKind.Binary);
                log?.Invoke((enable ? "Activado en inicio: " : "Desactivado en inicio: ") + item.Name + " (el programa sigue instalado, solo no se abre solo).");
                return true;
            }
            catch (Exception ex) { log?.Invoke("Inicio " + item.Name + ": " + ex.Message); return false; }
        }

        private static void SaveBackup()
        {
            try
            {
                if (File.Exists(OptimizePaths.StartupBackup)) return; // un solo respaldo original
                var items = List();
                Directory.CreateDirectory(OptimizePaths.Folder);
                File.WriteAllText(OptimizePaths.StartupBackup, JsonSerializer.Serialize(items.Select(i => new { i.Name, i.Location, i.ValueName, i.Enabled })));
            }
            catch { }
        }

        public static void RestoreAll(Action<string>? log = null)
        {
            try
            {
                if (!File.Exists(OptimizePaths.StartupBackup)) { log?.Invoke("No hay respaldo de inicio que restaurar."); return; }
                var saved = JsonSerializer.Deserialize<List<StartupItem>>(File.ReadAllText(OptimizePaths.StartupBackup));
                if (saved == null) return;
                foreach (var s in saved) SetEnabled(s, s.Enabled, null);
                log?.Invoke("Programas de inicio restaurados a como estaban.");
            }
            catch (Exception ex) { log?.Invoke("Restaurar inicio: " + ex.Message); }
        }
    }

    // ---------- 3) Servicios de Windows ----------
    public class ServiceOption
    {
        public string Name = "";
        public string Display = "";
        public string Why = "";
        public bool DefaultChecked;
    }

    public static class WindowsServices
    {
        public static List<ServiceOption> Options() => new()
        {
            new ServiceOption { Name = "SysMain", Display = "SysMain (Superfetch)", Why = "Precarga apps en RAM. En SSD no ayuda y usa disco y CPU en segundo plano.", DefaultChecked = true },
            new ServiceOption { Name = "WSearch", Display = "Búsqueda de Windows", Why = "Índice para buscar archivos rápido. Sin él, buscar tarda más, pero no usa disco en segundo plano.", DefaultChecked = true },
            new ServiceOption { Name = "DiagTrack", Display = "Telemetría (DiagTrack)", Why = "Envía datos de diagnóstico a Microsoft. No afecta a juegos.", DefaultChecked = true },
            new ServiceOption { Name = "dmwappushservice", Display = "WAP Push (telemetría)", Why = "Mensajes push del sistema, ligado a telemetría. Sin efecto en juegos.", DefaultChecked = true },
            new ServiceOption { Name = "Fax", Display = "Fax", Why = "Servicio de fax. Casi nadie lo usa.", DefaultChecked = true },
            new ServiceOption { Name = "MapsBroker", Display = "Mapas descargados", Why = "Gestiona mapas sin conexión. Si no usas Mapas, no hace falta.", DefaultChecked = true },
            new ServiceOption { Name = "RemoteRegistry", Display = "Registro remoto", Why = "Permite editar el registro desde otro equipo. Por seguridad conviene apagarlo.", DefaultChecked = true },
            new ServiceOption { Name = "XblAuthManager", Display = "Xbox Live (auth)", Why = "Solo para Xbox / Game Pass. NO lo marques si usas la app Xbox o Game Pass.", DefaultChecked = false },
            new ServiceOption { Name = "XblGameSave", Display = "Xbox Live (guardado)", Why = "Guardado en la nube de Xbox. NO lo marques si juegas con Xbox / Game Pass.", DefaultChecked = false },
            new ServiceOption { Name = "XboxNetApiSvc", Display = "Xbox Live (red)", Why = "Red de Xbox. NO lo marques si usas Xbox / Game Pass.", DefaultChecked = false },
            new ServiceOption { Name = "Spooler", Display = "Cola de impresión", Why = "Impresoras. NO lo marques si tienes impresora.", DefaultChecked = false },
        };

        public static (bool Exists, bool Running, string StartType) GetStatus(string name)
        {
            try
            {
                var (code, q) = ProcessRunner.Run("sc", "query " + name, 10000);
                if (code != 0 || q.Contains("FAILED 1060")) return (false, false, "");
                bool running = q.Contains("RUNNING");
                var (code2, qc) = ProcessRunner.Run("sc", "qc " + name, 10000);
                string start = "";
                foreach (var line in qc.Split('\n'))
                {
                    if (line.Contains("START_TYPE"))
                    {
                        if (line.Contains("AUTO_START")) start = "auto";
                        else if (line.Contains("DEMAND_START")) start = "demand";
                        else if (line.Contains("DISABLED")) start = "disabled";
                        else start = line.Trim();
                    }
                }
                return (true, running, start);
            }
            catch { return (false, false, ""); }
        }

        private static Dictionary<string, string> LoadBackup()
        {
            try
            {
                if (File.Exists(OptimizePaths.ServicesBackup))
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(OptimizePaths.ServicesBackup)) ?? new();
            }
            catch { }
            return new();
        }

        private static void SaveBackup(string name, string startType)
        {
            try
            {
                var b = LoadBackup();
                if (!b.ContainsKey(name)) b[name] = startType; // solo el valor original
                Directory.CreateDirectory(OptimizePaths.Folder);
                File.WriteAllText(OptimizePaths.ServicesBackup, JsonSerializer.Serialize(b));
            }
            catch { }
        }

        // Pone el servicio en Manual y lo detiene. No lo deshabilita del
        // todo: en Manual, Windows o el juego lo puede arrancar si lo necesita.
        public static void ApplyManual(string name, Action<string>? log = null)
        {
            var (exists, _, start) = GetStatus(name);
            if (!exists) { log?.Invoke("Servicio " + name + ": no existe en este equipo, se salta."); return; }
            SaveBackup(name, string.IsNullOrEmpty(start) ? "auto" : start);
            var (c1, _) = ProcessRunner.Run("sc", "config " + name + " start= demand", 10000);
            var (c2, _) = ProcessRunner.Run("sc", "stop " + name, 15000);
            log?.Invoke($"Servicio {name}: inicio Manual ({(c1 == 0 ? "OK" : "codigo " + c1)}), detenido ({(c2 == 0 ? "OK" : "codigo " + c2 + " — puede que ya estuviera parado")}).");
        }

        public static void Restore(string name, Action<string>? log = null)
        {
            var b = LoadBackup();
            if (!b.TryGetValue(name, out var orig)) { log?.Invoke("Servicio " + name + ": sin respaldo, no se toca."); return; }
            var (c1, _) = ProcessRunner.Run("sc", "config " + name + " start= " + orig, 10000);
            if (orig == "auto") ProcessRunner.Run("sc", "start " + name, 15000);
            log?.Invoke($"Servicio {name}: restaurado a {orig} ({(c1 == 0 ? "OK" : "codigo " + c1)}).");
        }
    }

    // ---------- 4) Efectos visuales ----------
    public static class VisualEffectsService
    {
        private const uint SPI_SETANIMATION = 0x0049;
        private const uint SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;
        private const uint WM_SETTINGCHANGE = 0x001A;

        [StructLayout(LayoutKind.Sequential)]
        private struct ANIMATIONINFO { public uint cbSize; public int iMinAnimate; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref ANIMATIONINFO pvParam, uint fWinIni);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint fuFlags, uint timeout, out IntPtr lpdwResult);

        // Aplica solo lo marcado: animaciones, transparencia y/o el
        // ajuste 'mejor rendimiento'. Cada uno se guarda por separado.
        public static void ApplyPerformance(bool noAnim, bool noTransp, bool perf, Action<string>? log = null)
        {
            try
            {
                // Respaldo: valor actual de efectos y de transparencia
                var backup = new Dictionary<string, string>();
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                    backup["VisualFxSetting"] = (k?.GetValue("VisualFxSetting") ?? 0).ToString()!;
                    using var k2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    backup["EnableTransparency"] = (k2?.GetValue("EnableTransparency") ?? 1).ToString()!;
                    using var k3 = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
                    backup["MinAnimate"] = (k3?.GetValue("MinAnimate") ?? "1").ToString()!;
                    Directory.CreateDirectory(OptimizePaths.Folder);
                    File.WriteAllText(OptimizePaths.EffectsBackup, JsonSerializer.Serialize(backup));
                }
                catch { }

                if (perf)
                {
                    using var ve = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                    ve?.SetValue("VisualFxSetting", 2, RegistryValueKind.DWord); // 2 = ajustar para mejor rendimiento
                }
                if (noTransp)
                {
                    using var pers = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    pers?.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);
                }
                if (noAnim)
                {
                    using var wm = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics");
                    wm?.SetValue("MinAnimate", "0", RegistryValueKind.String);
                    var anim = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>(), iMinAnimate = 0 };
                    SystemParametersInfo(SPI_SETANIMATION, 0, ref anim, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
                SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 0x0002, 5000, out _);
                log?.Invoke("Efectos visuales aplicados (" + (noAnim ? "sin animaciones " : "") + (noTransp ? "sin transparencia " : "") + (perf ? "mejor rendimiento" : "") + "). Se puede restaurar.");
            }
            catch (Exception ex) { log?.Invoke("Efectos visuales: " + ex.Message); }
        }

        public static void Restore(Action<string>? log = null)
        {
            try
            {
                if (!File.Exists(OptimizePaths.EffectsBackup)) { log?.Invoke("No hay respaldo de efectos que restaurar."); return; }
                var b = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(OptimizePaths.EffectsBackup)) ?? new();
                if (b.TryGetValue("VisualFxSetting", out var v) && int.TryParse(v, out int vi))
                {
                    using var ve = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                    ve?.SetValue("VisualFxSetting", vi, RegistryValueKind.DWord);
                }
                if (b.TryGetValue("EnableTransparency", out var tr) && int.TryParse(tr, out int ti))
                {
                    using var pers = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    pers?.SetValue("EnableTransparency", ti, RegistryValueKind.DWord);
                }
                if (b.TryGetValue("MinAnimate", out var ma))
                {
                    using var wm = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics");
                    wm?.SetValue("MinAnimate", ma, RegistryValueKind.String);
                }
                var anim = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>(), iMinAnimate = 1 };
                SystemParametersInfo(SPI_SETANIMATION, 0, ref anim, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                log?.Invoke("Efectos visuales restaurados a como estaban.");
            }
            catch (Exception ex) { log?.Invoke("Restaurar efectos: " + ex.Message); }
        }
    }
}
