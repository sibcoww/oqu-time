using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace SchoolScheduler.ImportExport;

public sealed record TeacherImportResult(IReadOnlyList<string> Names, IReadOnlyList<string> Errors);

public sealed partial class TeacherExcelService
{
    public TeacherImportResult Import(string path)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        try
        {
            using var workbook = new XLWorkbook(path);
            var recognizedSheet = false;
            foreach (var sheet in workbook.Worksheets)
            {
                var header = FindTeacherHeader(sheet);
                if (header is null) continue;
                recognizedSheet = true;
                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.Value.Row;
                for (var row = header.Value.Row + 1; row <= lastRow; row++)
                {
                    var name = NormalizeName(sheet.Cell(row, header.Value.Column).GetString());
                    if (name.Length > 0) names.Add(name);
                }
            }
            if (!recognizedSheet)
                errors.Add("Не найдена колонка «ФИО учителя». Поддерживаются заголовки «ФИО учителя» и «Фамилия имя отчество учителя».");
            else if (names.Count == 0)
                errors.Add("В колонке «ФИО учителя» не найдено ни одного имени.");
        }
        catch (Exception ex) { errors.Add($"Не удалось прочитать Excel-файл: {ex.Message}"); }
        return new(names.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(), errors);
    }

    private static (int Row, int Column)? FindTeacherHeader(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed();
        if (used is null) return null;
        var lastHeaderRow = Math.Min(used.LastRow().RowNumber(), 20);
        foreach (var cell in sheet.Range(used.FirstRow().RowNumber(), used.FirstColumn().ColumnNumber(),
                     lastHeaderRow, used.LastColumn().ColumnNumber()).Cells())
        {
            var value = NormalizeName(cell.GetString()).ToLowerInvariant();
            if (value == "фио учителя" || value.Contains("фамилия имя отчество учителя") ||
                value.Contains("мұғалімнің аты-жөні"))
                return (cell.Address.RowNumber, cell.Address.ColumnNumber);
        }
        return null;
    }

    private static string NormalizeName(string value) => Whitespace().Replace(value.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
