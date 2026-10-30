using System.IO;
using System.Text.Json;

namespace Aimmy2.Other
{
    public record RecoilStep(double Dx, double Dy);

    public class RecoilPattern
    {
        public double Rpm { get; set; } = 600;
        public List<RecoilStep> Steps { get; set; } = new();
    }

    /// <summary>
    /// Loads/saves named recoil patterns as JSON files under bin/recoil/. Each
    /// pattern holds its own RPM (used to time shots while the Fire Keybind is
    /// held) and a list of per-shot pixel pulls applied by MouseManager's
    /// recoil-compensation loop.
    /// </summary>
    public static class RecoilManager
    {
        private const string RecoilDirectory = "bin/recoil";

        public static RecoilPattern? CurrentPattern { get; private set; }
        public static string CurrentPatternName { get; private set; } = "None";

        public static List<string> ListAvailablePatterns()
        {
            try
            {
                Directory.CreateDirectory(RecoilDirectory);
                return Directory.GetFiles(RecoilDirectory, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => n!)
                    .OrderBy(n => n)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void LoadPattern(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "None")
            {
                CurrentPattern = null;
                CurrentPatternName = "None";
                return;
            }

            try
            {
                string path = Path.Combine(RecoilDirectory, name + ".json");
                if (!File.Exists(path))
                {
                    CurrentPattern = null;
                    CurrentPatternName = "None";
                    return;
                }

                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                CurrentPattern = JsonSerializer.Deserialize<RecoilPattern>(json, options) ?? new RecoilPattern();
                CurrentPatternName = name;
            }
            catch
            {
                CurrentPattern = null;
                CurrentPatternName = "None";
            }
        }

        public static bool SavePattern(string name, RecoilPattern pattern)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "None") return false;

            try
            {
                Directory.CreateDirectory(RecoilDirectory);
                string path = Path.Combine(RecoilDirectory, name + ".json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(pattern, options);
                File.WriteAllText(path, json);

                if (name == CurrentPatternName)
                {
                    CurrentPattern = pattern;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void DeletePattern(string name)
        {
            try
            {
                string path = Path.Combine(RecoilDirectory, name + ".json");
                if (File.Exists(path)) File.Delete(path);

                if (name == CurrentPatternName)
                {
                    CurrentPattern = null;
                    CurrentPatternName = "None";
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
