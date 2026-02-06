using System;
using System.IO;
using System.Text.Json;
using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public static class StorageService
    {
        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HomeoMahanagarLabelCleanV2", "appdata.json");

        private static readonly JsonSerializerOptions Options =
            new() { WriteIndented = true };

        public static AppStorage Load()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(FilePath))
                    return new AppStorage();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppStorage>(json, Options)
                       ?? new AppStorage();
            }
            catch
            {
                // If storage cannot be read, return empty storage to allow app to continue
                return new AppStorage();
            }
        }

        public static void Save(AppStorage data)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(data, Options);
                File.WriteAllText(FilePath, json);
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
                // swallow - do not crash app on storage save failure
            }
        }
    }
}
