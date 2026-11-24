using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameBoost
{
    /// <summary>
    /// Aviso de nueva version (v1.8.0): consulta la ultima release de
    /// GitHub (AngelRM1/GameBoost) y dice si hay una version mas nueva
    /// que la instalada. Si no hay internet o GitHub no responde, no
    /// hace nada: nunca bloquea ni molesta.
    /// </summary>
    public record UpdateInfo(string Tag, string Url);

    public static class UpdateCheck
    {
        public const string RepoOwner = "AngelRM1";
        public const string RepoName = "GameBoost";
        public static string LatestReleaseApi => $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

        /// <summary>¿La etiqueta (p. ej. "v1.8.0") es mas nueva que la version instalada?</summary>
        public static bool IsNewer(string tag, Version current)
        {
            var clean = (tag ?? "").Trim().TrimStart('v', 'V');
            if (!Version.TryParse(clean, out var latest)) return false;
            return latest > current;
        }

        /// <summary>Extrae etiqueta y URL de la respuesta JSON de GitHub.</summary>
        public static UpdateInfo? ParseReleaseJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tag = root.GetProperty("tag_name").GetString() ?? "";
                var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(tag)) return null;
                return new UpdateInfo(tag, url);
            }
            catch { return null; }
        }

        /// <summary>Consulta GitHub. Devuelve la info si hay una version mas nueva; null si no hay o si falla.</summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.Add("User-Agent", "GameBoost");
                http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                var json = await http.GetStringAsync(LatestReleaseApi);
                var info = ParseReleaseJson(json);
                if (info == null) return null;
                return IsNewer(info.Tag, CurrentVersion) ? info : null;
            }
            catch { return null; }
        }
    }
}
