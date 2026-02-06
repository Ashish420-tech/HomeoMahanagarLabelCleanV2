using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public static class LabelLayoutService
    {
        public static List<LabelTextItem> Load(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<LabelTextItem>();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<LabelTextItem>>(json)
                   ?? new List<LabelTextItem>();
        }
    }
}
