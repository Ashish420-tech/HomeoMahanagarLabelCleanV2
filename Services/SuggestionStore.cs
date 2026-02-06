using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public class SuggestionStore
    {
        private static readonly string BaseFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "HomeoMahanagarLabel");

        private static readonly string FilePath =
            Path.Combine(BaseFolder, "Suggestions.json");

        public List<string> Potencies { get; set; } = new();
        public List<string> Doses { get; set; } = new();
        public List<string> Times { get; set; } = new();

        private static readonly JsonSerializerOptions _options =
            new JsonSerializerOptions { WriteIndented = true };

        public static SuggestionStore Load()
        {
            Directory.CreateDirectory(BaseFolder);

            if (!File.Exists(FilePath))
                return new SuggestionStore();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SuggestionStore>(json, _options)
                   ?? new SuggestionStore();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(BaseFolder);

                var json = JsonSerializer.Serialize(this, _options);
                File.WriteAllText(FilePath, json);
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
            }
        }
    }
}
