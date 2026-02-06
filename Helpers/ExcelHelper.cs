using ClosedXML.Excel;
using HomeoMahanagarLabelCleanV2.Models;
using System.Collections.Generic;
using System.Linq;

namespace HomeoMahanagarLabelCleanV2.Helpers
{
    public static class ExcelHelper
    {
        public static List<MedicineLabel> ReadExcel(string filePath)
        {
            var list = new List<MedicineLabel>();
            try
            {
                using var workbook = new XLWorkbook(filePath);
                var sheet = workbook.Worksheet(1);

                // Skip header row, read ONLY first two columns
                foreach (var row in sheet.RowsUsed().Skip(1))
                {
                    var latin = row.Cell(1).GetString().Trim();
                    var common = row.Cell(2).GetString().Trim();

                    // Safety: skip empty rows
                    if (string.IsNullOrWhiteSpace(latin) &&
                        string.IsNullOrWhiteSpace(common))
                        continue;

                    list.Add(new MedicineLabel
                    {
                        LatinName = latin,
                        CommonName = common
                    });
                }
            }
            catch (System.Exception ex)
            {
                try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex); } catch { }
            }

            return list;
        }
    }
}
