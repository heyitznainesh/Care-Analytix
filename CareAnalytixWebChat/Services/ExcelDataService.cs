using ClosedXML.Excel;

namespace CareAnalytixWebChat.Services
{
    public class ExcelDataService
    {
        private readonly string _excelFilePath;

        public ExcelDataService(IWebHostEnvironment env)
        {
            _excelFilePath = Path.Combine(env.ContentRootPath, "Models", "Training.xlsx");
        }

        public List<Dictionary<string, string>> GetSheetData(int sheetIndex = 1)
        {
            var data = new List<Dictionary<string, string>>();
            if (!File.Exists(_excelFilePath)) return data;

            using var workbook = new XLWorkbook(_excelFilePath);
            if (workbook.Worksheets.Count < sheetIndex) return data;

            var worksheet = workbook.Worksheet(sheetIndex);
            var range = worksheet.RangeUsed();
            if (range == null) return data;

            var rows = range.RowsUsed();

            var headers = new List<string>();
            bool isFirstRow = true;

            foreach (var row in rows)
            {
                if (isFirstRow)
                {
                    foreach (var cell in row.Cells())
                    {
                        headers.Add(cell.GetString().Trim());
                    }
                    isFirstRow = false;
                }
                else
                {
                    var rowData = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        rowData[headers[i]] = row.Cell(i + 1).GetString().Trim();
                    }
                    data.Add(rowData);
                }
            }

            return data;
        }
    }
}
