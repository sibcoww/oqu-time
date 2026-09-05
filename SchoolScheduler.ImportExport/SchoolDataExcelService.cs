using ClosedXML.Excel;

namespace SchoolScheduler.ImportExport;

public sealed record SchoolDataImportRow(int RowNumber, string Teacher, string Subject, string ShortSubject,
    string SchoolClass, decimal HoursPerWeek, string? Room, string? Group, bool AllowZeroLesson);

public sealed record SchoolDataImportFile(IReadOnlyList<SchoolDataImportRow> Rows, IReadOnlyList<string> Errors);

public sealed class SchoolDataExcelService
{
    public SchoolDataImportFile Import(string path)
    {
        var rows = new List<SchoolDataImportRow>();
        var errors = new List<string>();
        try
        {
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheets.FirstOrDefault(x =>
                x.Name.Equals("По классам", StringComparison.OrdinalIgnoreCase));
            var header = sheet is null ? null : FindHeader(sheet);
            if (header is null)
            {
                foreach (var candidate in workbook.Worksheets)
                {
                    var found = FindHeader(candidate);
                    if (found is null) continue;
                    sheet = candidate; header = found; break;
                }
            }
            if (sheet is null || header is null)
                return new([], ["Не найден лист с колонками «ФИО учителя», «Предмет», «Класс» и «Часов в неделю»."]);

            var columns = header.Value.Columns;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.Value.Row;
            for (var row = header.Value.Row + 1; row <= lastRow; row++)
            {
                var teacher = Text(sheet, row, columns["teacher"]);
                var subject = Text(sheet, row, columns["subject"]);
                var schoolClass = Text(sheet, row, columns["class"]);
                if (teacher.Length == 0 && subject.Length == 0 && schoolClass.Length == 0) continue;
                if (teacher.Length == 0 || subject.Length == 0 || schoolClass.Length == 0)
                { errors.Add($"Строка {row}: должны быть указаны учитель, предмет и класс."); continue; }
                if (!TryDecimal(sheet.Cell(row, columns["hours"]), out var hours) || hours <= 0)
                { errors.Add($"Строка {row}: неверно указано количество часов."); continue; }
                var shortName = columns.TryGetValue("short", out var shortColumn) ? Text(sheet, row, shortColumn) : subject;
                var room = columns.TryGetValue("room", out var roomColumn) ? NullText(sheet, row, roomColumn) : null;
                var group = columns.TryGetValue("group", out var groupColumn) ? NullText(sheet, row, groupColumn) : null;
                var zero = columns.TryGetValue("zero", out var zeroColumn) && IsYes(Text(sheet, row, zeroColumn));
                rows.Add(new(row, teacher, subject, shortName.Length == 0 ? subject : shortName,
                    schoolClass, hours, room, group, zero));
            }
        }
        catch (Exception ex) { errors.Add($"Не удалось прочитать Excel-файл: {ex.Message}"); }
        return new(rows, errors);
    }

    private static (int Row, Dictionary<string, int> Columns)? FindHeader(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed(); if (used is null) return null;
        var last = Math.Min(used.LastRow().RowNumber(), 20);
        for (var row = used.FirstRow().RowNumber(); row <= last; row++)
        {
            var columns = new Dictionary<string, int>();
            for (var column = used.FirstColumn().ColumnNumber(); column <= used.LastColumn().ColumnNumber(); column++)
            {
                var value = Text(sheet, row, column).ToLowerInvariant();
                if (value.Contains("фио учителя") || value.Contains("фамилия имя отчество учителя")) columns["teacher"] = column;
                else if (value == "предмет" || value.EndsWith("\nпредмет")) columns["subject"] = column;
                else if (value.Contains("короткое название")) columns["short"] = column;
                else if (value == "класс" || value.Contains("классы")) columns["class"] = column;
                else if (value.Contains("часов в неделю") || value.Contains("сколько часов в неделю")) columns["hours"] = column;
                else if (value == "кабинет") columns["room"] = column;
                else if (value == "группа" || value.EndsWith("\nгруппа")) columns["group"] = column;
                else if (value.Contains("нулевой урок")) columns["zero"] = column;
            }
            if (new[] { "teacher", "subject", "class", "hours" }.All(columns.ContainsKey)) return (row, columns);
        }
        return null;
    }

    private static bool TryDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue(out value)) return true;
        return decimal.TryParse(cell.GetString().Trim().Replace(',', '.'),
            System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
    private static string Text(IXLWorksheet sheet, int row, int column) =>
        string.Join(" ", sheet.Cell(row, column).GetString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string? NullText(IXLWorksheet sheet, int row, int column)
    { var value = Text(sheet, row, column); return value.Length == 0 || value == "—" ? null : value; }
    private static bool IsYes(string value) => value.Equals("да", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("иә", StringComparison.OrdinalIgnoreCase);
}
