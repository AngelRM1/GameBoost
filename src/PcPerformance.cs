using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameBoost
{
    /// <summary>
    /// Paquete recomendado v1.6.0: ajustes de rendimiento de Windows para
    /// juegos. Todo es reversible (el respaldo guarda los valores
    /// originales al primer cambio) salvo dos cosas que no se pueden
    /// deshacer pero tampoco se pierden: la cache de Windows Update
    /// (Windows vuelve a descargar lo que necesite) y el TRIM/defrag
    /// (mantenimiento del disco, no borra nada).
    /// </summary>

    // ---------- Respaldo ----------
    public class PerfBackup
    {
        public bool? GameMode { get; set; }
        public int? Hags { get; set; }
        public string PowerPlanGuid { get; set; } = "";
        public bool? GameDvr { get; set; }
        public bool? Hibernation { get; set; }
    }

    public static class PerfBackupStore
    {
        public static string Path_ => Path.Combine(BackupStore.Folder, "perf-backup.json");
        public static PerfBackup? Load()
        {
            try
            {
                if (!File.Exists(Path_)) return null;
                return JsonSerializer.Deserialize<PerfBackup>(File.ReadAllText(Path_));
            }
            catch { return null; }
        }
        public static void Save(PerfBackup b)
        {
            Directory.CreateDirectory(BackupStore.Folder);
            File.WriteAllText(Path_, JsonSerializer.Serialize(b, new JsonSerializerOptions { WriteIndented = true }));
        }
        /// <summary>Guarda el valor original solo la primera vez.</summary>
        public static void SaveOriginal(Action<PerfBackup> capture)
        {
            var b = Load() ?? new PerfBackup();
            capture(b);
            Save(b);
        }
    }

    // ---------- Modo Juego de Windows + HAGS + Game DVR ----------
    public static class WindowsGameTweaks
    {
        private const string GameBarKey = @"Software\Microsoft\GameBar";
        private const string GraphicsKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string GameConfigKey = @"System\GameConfigStore";

        public static bool GetGameMode()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(GameBarKey);
                return k != null && Convert.ToInt32(k.GetValue("AutoGameModeEnabled", 0)) == 1;
            }
            catch { return false; }
        }
        public static void SetGameMode(bool on, Action<string> log)
        {
            try
            {
                bool cur = GetGameMode();
                PerfBackupStore.SaveOriginal(b => { if (b.GameMode == null) b.GameMode = cur; });
                using var k = Registry.CurrentUser.CreateSubKey(GameBarKey);
                k.SetValue("AutoGameModeEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                log("Modo Juego de Windows: " + (on ? "activado." : "desactivado."));
            }
            catch (Exception ex) { log("Modo Juego: no se pudo cambiar (" + ex.Message + ")."); }
        }

        /// <summary>HwSchMode: 2 = activado, 1 = desactivado, falta = desactivado.</summary>
        public static int GetHags()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(GraphicsKey);
                if (k == null) return 1;
                return Convert.ToInt32(k.GetValue("HwSchMode", 1));
            }
            catch { return 1; }
        }
        public static void SetHags(bool on, Action<string> log)
        {
            try
            {
                int cur = GetHags();
                PerfBackupStore.SaveOriginal(b => { if (b.Hags == null) b.Hags = cur; });
                using var k = Registry.LocalMachine.CreateSubKey(GraphicsKey);
                k.SetValue("HwSchMode", on ? 2 : 1, RegistryValueKind.DWord);
                log("HAGS: " + (on ? "activado (se aplica al reiniciar la PC)." : "desactivado (se aplica al reiniciar la PC)."));
            }
            catch (Exception ex) { log("HAGS: no se pudo cambiar (" + ex.Message + "). Ejecuta como administrador."); }
        }

        /// <summary>Game DVR viene activado de fabrica: si falta el valor, es que esta activado.</summary>
        public static bool GetGameDvr()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(GameDvrKey);
                if (k != null) return Convert.ToInt32(k.GetValue("AppCaptureEnabled", 1)) == 1;
                using var k2 = Registry.CurrentUser.OpenSubKey(GameConfigKey);
                if (k2 != null) return Convert.ToInt32(k2.GetValue("GameDVR_Enabled", 1)) == 1;
                return true;
            }
            catch { return true; }
        }
        public static void SetGameDvr(bool enabled, Action<string> log)
        {
            try
            {
                bool cur = GetGameDvr();
                PerfBackupStore.SaveOriginal(b => { if (b.GameDvr == null) b.GameDvr = cur; });
                using var k = Registry.CurrentUser.CreateSubKey(GameDvrKey);
                k.SetValue("AppCaptureEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                using var k2 = Registry.CurrentUser.CreateSubKey(GameConfigKey);
                k2.SetValue("GameDVR_Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                log("Game DVR: " + (enabled ? "activado." : "desactivado."));
            }
            catch (Exception ex) { log("Game DVR: no se pudo cambiar (" + ex.Message + ")."); }
        }

        public static void RestoreAll(Action<string> log)
        {
            var b = PerfBackupStore.Load();
            if (b == null) { log("Rendimiento: no hay nada que restaurar (no se cambio nada)."); return; }
            if (b.GameMode != null) SetGameModeNoBackup(b.GameMode.Value, log);
            if (b.Hags != null) SetHagsNoBackup(b.Hags.Value, log);
            if (b.GameDvr != null) SetGameDvrNoBackup(b.GameDvr.Value, log);
            if (!string.IsNullOrEmpty(b.PowerPlanGuid)) PowerPlanService.SetActive(b.PowerPlanGuid, log);
            if (b.Hibernation != null) HibernationService.Set(b.Hibernation.Value, log, false);
            log("Rendimiento restaurado a como estaba.");
        }
        private static void SetGameModeNoBackup(bool on, Action<string> log)
        {
            try { using var k = Registry.CurrentUser.CreateSubKey(GameBarKey); k.SetValue("AutoGameModeEnabled", on ? 1 : 0, RegistryValueKind.DWord); log("Modo Juego restaurado: " + (on ? "activado." : "desactivado.")); }
            catch (Exception ex) { log("Modo Juego: no se pudo restaurar (" + ex.Message + ")."); }
        }
        private static void SetHagsNoBackup(int v, Action<string> log)
        {
            try { using var k = Registry.LocalMachine.CreateSubKey(GraphicsKey); k.SetValue("HwSchMode", v, RegistryValueKind.DWord); log("HAGS restaurado."); }
            catch (Exception ex) { log("HAGS: no se pudo restaurar (" + ex.Message + ")."); }
        }
        private static void SetGameDvrNoBackup(bool on, Action<string> log)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(GameDvrKey); k.SetValue("AppCaptureEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                using var k2 = Registry.CurrentUser.CreateSubKey(GameConfigKey); k2.SetValue("GameDVR_Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                log("Game DVR restaurado: " + (on ? "activado." : "desactivado."));
            }
            catch (Exception ex) { log("Game DVR: no se pudo restaurar (" + ex.Message + ")."); }
        }
    }

    // ---------- Plan de energia ----------
    public static class PowerPlanService
    {
        public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
        public const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        private static readonly Regex GuidRx = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

        public static string? ParseGuid(string output)
        {
            var m = GuidRx.Match(output ?? "");
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        public static string FriendlyName(string? guid) => guid switch
        {
            BalancedGuid => "Equilibrado",
            HighPerfGuid => "Alto rendimiento",
            PowerSaverGuid => "Ahorro de energia",
            UltimateGuid => "Maximo rendimiento",
            null or "" => "sin detectar",
            _ => "Plan personalizado",
        };

        public static string? GetActiveGuid()
        {
            var (_, outText) = ProcessRunner.Run("powercfg", "/getactivescheme");
            return ParseGuid(outText);
        }

        public static string GetActiveName()
        {
            var g = GetActiveGuid();
            return FriendlyName(g) + (g == null ? "" : "  (" + g + ")");
        }

        /// <summary>Crea el plan Maximo rendimiento si no existe y devuelve su GUID.</summary>
        public static string? EnsureUltimate(Action<string> log)
        {
            var (_, list) = ProcessRunner.Run("powercfg", "/list");
            if (!string.IsNullOrEmpty(list) && list.IndexOf(UltimateGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                return UltimateGuid;
            var (code, outText) = ProcessRunner.Run("powercfg", "/duplicatescheme " + UltimateGuid);
            var g = ParseGuid(outText);
            if (code != 0 || g == null) { log("Plan Maximo rendimiento: no se pudo crear (" + outText.Trim() + ")."); return null; }
            return g;
        }

        public static void SetActive(string guid, Action<string> log)
        {
            var (code, outText) = ProcessRunner.Run("powercfg", "/setactive " + guid);
            if (code == 0) log("Plan de energia: " + FriendlyName(guid.ToLowerInvariant()) + ".");
            else log("Plan de energia: no se pudo cambiar (" + outText.Trim() + ").");
        }

        public static void ApplyUltimate(Action<string> log)
        {
            var cur = GetActiveGuid();
            PerfBackupStore.SaveOriginal(b => { if (string.IsNullOrEmpty(b.PowerPlanGuid) && cur != null) b.PowerPlanGuid = cur; });
            var g = EnsureUltimate(log);
            if (g != null) SetActive(g, log);
        }

        public static void ApplyBalanced(Action<string> log)
        {
            var cur = GetActiveGuid();
            PerfBackupStore.SaveOriginal(b => { if (string.IsNullOrEmpty(b.PowerPlanGuid) && cur != null) b.PowerPlanGuid = cur; });
            SetActive(BalancedGuid, log);
        }
    }

    // ---------- Hibernacion ----------
    public static class HibernationService
    {
        private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Power";

        public static bool IsEnabled()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(PowerKey);
                if (k != null) return Convert.ToInt32(k.GetValue("HibernateEnabled", 0)) == 1;
            }
            catch { }
            return File.Exists(HiberFilePath());
        }

        public static string HiberFilePath()
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            return Path.Combine(root, "hiberfil.sys");
        }

        public static double? HiberFileGb()
        {
            try
            {
                var f = new FileInfo(HiberFilePath());
                if (!f.Exists) return null;
                return f.Length / (1024.0 * 1024 * 1024);
            }
            catch { return null; }
        }

        public static void Set(bool on, Action<string> log, bool backup = true)
        {
            try
            {
                if (backup)
                {
                    bool cur = IsEnabled();
                    PerfBackupStore.SaveOriginal(b => { if (b.Hibernation == null) b.Hibernation = cur; });
                }
                var (code, outText) = ProcessRunner.Run("powercfg", on ? "/hibernate on" : "/hibernate off");
                if (code == 0) log("Hibernacion: " + (on ? "encendida." : "apagada (archivo hiberfil.sys eliminado)."));
                else log("Hibernacion: no se pudo cambiar (" + outText.Trim() + "). Ejecuta como administrador.");
            }
            catch (Exception ex) { log("Hibernacion: " + ex.Message); }
        }
    }

    // ---------- Cache de Windows Update ----------
    public static class UpdateCacheService
    {
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");

        public static long Measure()
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static (int Deleted, int Skipped, long Freed) Clean(Action<string> log)
        {
            int deleted = 0, skipped = 0; long freed = 0;
            try
            {
                ProcessRunner.RunCmd("net stop wuauserv");
                foreach (var f in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
                {
                    try { var fi = new FileInfo(f); freed += fi.Length; File.Delete(f); deleted++; }
                    catch { skipped++; }
                }
                foreach (var d in Directory.EnumerateDirectories(Folder, "*", SearchOption.AllDirectories).Reverse())
                {
                    try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
                }
            }
            catch (Exception ex) { log("Cache de Update: " + ex.Message); }
            finally { try { ProcessRunner.RunCmd("net start wuauserv"); } catch { } }
            log($"Cache de Windows Update: {deleted} archivos borrados, {OptimizePaths.FormatBytes(freed)} liberados, {skipped} saltados.");
            return (deleted, skipped, freed);
        }
    }

    // ---------- Salud del disco ----------
    public class DiskInfo
    {
        public string Letters = "";
        public string Model = "";
        public string Type = "Disco";   // SSD, HDD o Disco (sin detectar)
        public double SizeGb;
        public string Status = "";
        public bool Healthy = true;
    }

    public static class DiskHealthService
    {
        public static List<DiskInfo> ListDisks()
        {
            var result = new List<DiskInfo>();
            try
            {
                // Tipo SSD/HDD por indice fisico (MSFT_PhysicalDisk).
                var mediaByIndex = new Dictionary<int, string>();
                try
                {
                    var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                    scope.Connect();
                    var q = new ObjectQuery("SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk");
                    using var s = new ManagementObjectSearcher(scope, q);
                    foreach (ManagementObject o in s.Get())
                    {
                        int idx = Convert.ToInt32(o["DeviceId"]);
                        int mt = Convert.ToInt32(o["MediaType"]);
                        mediaByIndex[idx] = mt == 4 ? "SSD" : mt == 3 ? "HDD" : mt == 5 ? "SCM" : "Disco";
                    }
                }
                catch { /* sin acceso a Storage: tipo sin detectar */ }

                using var searcher = new ManagementObjectSearcher("SELECT Index, Model, Size, Status FROM Win32_DiskDrive");
                foreach (ManagementObject d in searcher.Get())
                {
                    int idx = Convert.ToInt32(d["Index"]);
                    var letters = new List<string>();
                    try
                    {
                        var parts = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{d["DeviceID"]}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition").Get();
                        foreach (ManagementObject p in parts)
                        {
                            var logicals = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{p["DeviceID"]}'}} WHERE AssocClass = Win32_LogicalDiskToPartition").Get();
                            foreach (ManagementObject l in logicals) letters.Add(l["DeviceID"]?.ToString() ?? "");
                        }
                    }
                    catch { }
                    if (letters.Count == 0) continue; // disco sin letra (recuperacion, etc.)
                    string status = d["Status"]?.ToString() ?? "";
                    result.Add(new DiskInfo
                    {
                        Letters = string.Join(", ", letters),
                        Model = (d["Model"]?.ToString() ?? "").Trim(),
                        Type = mediaByIndex.TryGetValue(idx, out var t) ? t : "Disco",
                        SizeGb = d["Size"] == null ? 0 : Convert.ToUInt64(d["Size"]) / (1024.0 * 1024 * 1024),
                        Status = status,
                        Healthy = string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase),
                    });
                }
            }
            catch { }
            return result;
        }

        /// <summary>Extrae el % de fragmentacion de la salida de defrag /A (sirve en espanol e ingles).</summary>
        public static int? ParseFragmentation(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            // Buscar la linea de fragmentacion ("Total fragmented space" /
            // "Espacio total fragmentado"): otras lineas tambien llevan %
            // y el ultimo % del texto no es el de fragmentacion.
            foreach (var line in output.Split('\n'))
            {
                if (line.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var m = Regex.Match(line, @"(\d+)\s*%");
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
            return null;
        }

        public static (bool Ok, int? Percent, string Raw) Analyze(string letter)
        {
            var (code, outText) = ProcessRunner.Run("defrag", letter + " /A", 120000);
            if (code != 0) return (false, null, outText);
            return (true, ParseFragmentation(outText), outText);
        }

        /// <summary>TRIM en SSD, desfragmentar en HDD. Puede tardar varios minutos: llamar en segundo plano.</summary>
        public static (int ExitCode, string Output) Optimize(string letter)
        {
            return ProcessRunner.Run("defrag", letter + " /O", 3600000);
        }
    }
}
