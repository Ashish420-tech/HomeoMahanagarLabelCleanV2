using HomeoMahanagarLabelCleanV2.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public class MedicineService
    {
        private readonly string _filePath = "Data/medicines.json";

        public List<Medicine> LoadMedicines()
        {
            if (!File.Exists(_filePath))
                return new List<Medicine>();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Medicine>>(json) ?? new List<Medicine>();
        }

        public void SaveMedicines(IEnumerable<Medicine> medicines)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(medicines, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }
    }
}
