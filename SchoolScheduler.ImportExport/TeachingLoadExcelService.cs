using ClosedXML.Excel;

namespace SchoolScheduler.ImportExport;

public sealed record TeachingLoadTemplateData(IReadOnlyCollection<string> Teachers,
    IReadOnlyCollection<string> Subjects, IReadOnlyCollection<string> Classes,
    IReadOnlyCollection<string> Groups, IReadOnlyCollection<string> Rooms);

public sealed record TeachingLoadImportRow(int RowNumber, string Teacher, string Subject,
    string SchoolClass, string? Group, decimal HoursPerWeek, string? Room,
    bool AllowZeroLesson, string Comment);

public sealed record TeachingLoadImportResult(IReadOnlyList<TeachingLoadImportRow> Rows,
    IReadOnlyList<string> Errors);

public sealed class TeachingLoadExcelService
{
    public const string SheetName = "Нагрузка";
    public static readonly string[] Headers =
    ["Учитель", "Предмет", "Класс", "Группа", "Часов/нед", "Кабинет", "Нулевой урок", "Комментарий"];

    public void CreateTemplate(string path, TeachingLoadTemplateData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);
        for (var column = 1; column <= Headers.Length; column++) sheet.Cell(1, column).Value = Headers[column - 1];
        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.Bold = true; header.Style.Font.FontColor = XLColor.White;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1); sheet.Range(1, 1, 200, Headers.Length).SetAutoFilter();
        sheet.Column(1).Width = 28; sheet.Column(2).Width = 22; sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 16; sheet.Column(5).Width = 12; sheet.Column(6).Width = 14;
        sheet.Column(7).Width = 16; sheet.Column(8).Width = 32;
        sheet.Range("E2:E200").Style.NumberFormat.Format = "0.00";
        sheet.Range("G2:G200").CreateDataValidation().List("Да,Нет");
        sheet.Cell("A2").Value = "Бакенова Ж.А."; sheet.Cell("B2").Value = "Математика";
        sheet.Cell("C2").Value = "7Б"; sheet.Cell("E2").Value = 5m; sheet.Cell("G2").Value = "Нет";

        var refs = workbook.Worksheets.Add("Справочники");
        WriteReference(refs, 1, "Учителя", data.Teachers); WriteReference(refs, 2, "Предметы", data.Subjects);
        WriteReference(refs, 3, "Классы", data.Classes); WriteReference(refs, 4, "Группы", data.Groups);
        WriteReference(refs, 5, "Кабинеты", data.Rooms); refs.Columns(1, 5).AdjustToContents();
        refs.Style.Protection.SetLocked(true); refs.Protect();
        workbook.SaveAs(path);
    }

    public TeachingLoadImportResult Import(string path)
    {
        var rows = new List<TeachingLoadImportRow>(); var errors = new List<string>();
        try
        {
            using var workbook = new XLWorkbook(path);
            if (!workbook.TryGetWorksheet(SheetName, out var sheet))
                return new([], [$"Отсутствует обязательный лист «{SheetName}»."]);
            for (var column = 1; column <= Headers.Length; column++)
                if (!string.Equals(sheet.Cell(1, column).GetString().Trim(), Headers[column - 1], StringComparison.Ordinal))
                    errors.Add($"Колонка {column}: ожидается заголовок «{Headers[column - 1]}».");
            if (errors.Count > 0) return new([], errors);

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (sheet.Row(row).Cells(1, Headers.Length).All(x => x.IsEmpty())) continue;
                var teacher = Text(sheet, row, 1); var subject = Text(sheet, row, 2); var schoolClass = Text(sheet, row, 3);
                if (teacher.Length == 0) errors.Add($"Строка {row}: не указан учитель.");
                if (subject.Length == 0) errors.Add($"Строка {row}: не указан предмет.");
                if (schoolClass.Length == 0) errors.Add($"Строка {row}: не указан класс.");
                if (!sheet.Cell(row, 5).TryGetValue<decimal>(out var hours) || hours <= 0)
                { errors.Add($"Строка {row}: часы должны быть положительным числом."); continue; }
                var zeroText = Text(sheet, row, 7);
                if (!TryParseBoolean(zeroText, out var zero))
                { errors.Add($"Строка {row}: «Нулевой урок» должен быть «Да» или «Нет»."); continue; }
                rows.Add(new(row, teacher, subject, schoolClass, NullText(sheet, row, 4), hours,
                    NullText(sheet, row, 6), zero, Text(sheet, row, 8)));
            }
        }
        catch (Exception ex) { errors.Add($"Не удалось прочитать файл: {ex.Message}"); }
        return new(rows, errors);
    }

    private static void WriteReference(IXLWorksheet sheet, int column, string header, IEnumerable<string> values)
    { sheet.Cell(1, column).Value = header; sheet.Cell(1, column).Style.Font.Bold = true; var row = 2; foreach (var value in values) sheet.Cell(row++, column).Value = value; }
    private static string Text(IXLWorksheet sheet, int row, int column) => sheet.Cell(row, column).GetString().Trim();
    private static string? NullText(IXLWorksheet sheet, int row, int column) { var value = Text(sheet, row, column); return value.Length == 0 || value == "—" ? null : value; }
    private static bool TryParseBoolean(string value, out bool result)
    { if (value.Equals("Да", StringComparison.OrdinalIgnoreCase)) { result = true; return true; } if (value.Equals("Нет", StringComparison.OrdinalIgnoreCase)) { result = false; return true; } result = false; return false; }
}
