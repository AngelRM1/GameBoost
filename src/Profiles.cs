using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GameBoost
{
    /// <summary>
    /// Perfiles por juego (v1.7.0): cada juego guarda sus ajustes de
    /// Modo Juego (que apps cerrar, plan de energia, pausa de Update y
    /// bloqueo de teclas). Se guardan en %AppData%\GameBoost\profiles.json
    /// junto con si el modo automatico esta activado.
    /// </summary>
    public class GameProfile
    {
        public string Name { get; set; } = "";
        public string Exe { get; set; } = "";
        public bool CloseApps { get; set; } = true;
        public bool Chrome { get; set; } = true;
        public bool Edge { get; set; } = true;
        public bool Firefox { get; set; } = true;
        public bool OneDrive { get; set; } = true;
        public bool Spotify { get; set; } = true;
        public bool Discord { get; set; } = false;
        public bool PowerPlan { get; set; } = true;
        public bool PauseUpdate { get; set; } = true;
        public bool BlockKeys { get; set; } = false;

        /// <summary>Resumen corto para mostrar en la lista de perfiles.</summary>
        public string Summary()
        {
            var apps = new List<string>();
            if (!CloseApps) apps.Add("no cierra apps");
            else
            {
                if (Chrome) apps.Add("Chrome");
                if (Edge) apps.Add("Edge");
                if (Firefox) apps.Add("Firefox");
                if (OneDrive) apps.Add("OneDrive");
                if (Spotify) apps.Add("Spotify");
                if (Discord) apps.Add("Discord");
                if (apps.Count == 0) apps.Add("no cierra apps");
            }
            var opts = new List<string>();
            if (PowerPlan) opts.Add("Alto rendimiento");
            if (PauseUpdate) opts.Add("pausa Update");
            if (BlockKeys) opts.Add("bloquea tecla Windows");
            return "Cierra: " + string.Join(", ", apps) + (opts.Count > 0 ? "  •  " + string.Join(", ", opts) : "");
        }
    }

    public class ProfilesFile
    {
        public List<GameProfile> Profiles { get; set; } = new();
        public bool AutoMode { get; set; }
    }

    public static class ProfileStore
    {
        public static string Path_ => Path.Combine(BackupStore.Folder, "profiles.json");

        public static ProfilesFile Load()
        {
            try
            {
                if (!File.Exists(Path_)) return new ProfilesFile();
                return JsonSerializer.Deserialize<ProfilesFile>(File.ReadAllText(Path_)) ?? new ProfilesFile();
            }
            catch { return new ProfilesFile(); }
        }

        public static void Save(ProfilesFile f)
        {
            Directory.CreateDirectory(BackupStore.Folder);
            File.WriteAllText(Path_, JsonSerializer.Serialize(f, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>Nombre de proceso sin .exe, para buscarlo entre los procesos en ejecucion.</summary>
        public static string ExeBase(string exe)
        {
            var name = (exe ?? "").Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            return name;
        }
    }
}
