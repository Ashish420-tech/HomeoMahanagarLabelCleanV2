using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace HomeoMahanagarLabelCleanV2
{
    public class ExcelLayoutReader
    {
        public static List<LabelLineConfig> ReadLayout(string filePath)
        {
            List<LabelLineConfig> list = new List<LabelLineConfig>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var sheet = workbook.Worksheet(1);
                var rows = sheet.RowsUsed();

                foreach (var row in rows.Skip(1)) // skip header
                {
                    list.Add(new LabelLineConfig
                    {
                        LineNo = row.Cell(1).GetValue<int>(),
                        TextKey = row.Cell(2).GetValue<string>(),
                        Bold = row.Cell(3).GetValue<string>() == "TRUE",
                        FontSize = row.Cell(4).GetValue<int>(),
                        Align = row.Cell(5).GetValue<string>(),
                        LineSpacing = row.Cell(6).GetValue<int>()
                    });
                }
            }

            return list;
        }
    }
}
