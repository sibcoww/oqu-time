using ClosedXML.Excel;

namespace SchoolScheduler.ImportExport;

public sealed record ScheduleExportRow(int Day, int LessonNumber, string SchoolClass,
    string? Group, string Subject, string Teacher, string? Room, string? Shift = null);

public sealed class ScheduleExcelService
{
    private static readonly string[] Days =
        ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"];

    public void Export(string path, IReadOnlyCollection<ScheduleExportRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(rows);
        using var workbook = new XLWorkbook();
        WriteGroupedSheet(workbook, "По классам", "Класс", rows, x => x.SchoolClass,
            ["День", "Урок", "Предмет", "Группа", "Учитель", "Кабинет"],
            x => [DayName(x.Day), x.LessonNumber, x.Subject, x.Group ?? "—", x.Teacher, x.Room ?? "—"]);
        WriteGroupedSheet(workbook, "По учителям", "Учитель", rows, x => x.Teacher,
            ["День", "Урок", "Класс", "Группа", "Предмет", "Кабинет"],
            x => [DayName(x.Day), x.LessonNumber, x.SchoolClass, x.Group ?? "—", x.Subject, x.Room ?? "—"]);
        WriteGroupedSheet(workbook, "По кабинетам", "Кабинет", rows.Where(x => !string.IsNullOrWhiteSpace(x.Room)), x => x.Room!,
            ["День", "Урок", "Класс", "Группа", "Предмет", "Учитель"],
            x => [DayName(x.Day), x.LessonNumber, x.SchoolClass, x.Group ?? "—", x.Subject, x.Teacher]);
        WriteFullSheet(workbook, rows);
        workbook.SaveAs(path);
    }

    private static void WriteGroupedSheet(XLWorkbook workbook, string sheetName, string resourceLabel,
        IEnumerable<ScheduleExportRow> source, Func<ScheduleExportRow, string> resource,
        string[] headers, Func<ScheduleExportRow, object[]> values)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        var row = 1;
        foreach (var group in source.GroupBy(resource).OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var title = sheet.Range(row, 1, row, headers.Length).Merge();
            title.Value = $"{resourceLabel}: {group.Key}";
            title.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 13;
            row++;
            WriteHeaders(sheet, row++, headers);
            foreach (var lesson in group.OrderBy(x => x.Day).ThenBy(x => x.LessonNumber))
                WriteValues(sheet, row++, values(lesson));
            row++;
        }
        if (row == 1) sheet.Cell(1, 1).Value = "В расписании нет занятий для этого представления.";
        FinishSheet(sheet, headers.Length);
    }

    private static void WriteFullSheet(XLWorkbook workbook, IEnumerable<ScheduleExportRow> source)
    {
        string[] headers = ["День", "Урок", "Класс", "Группа", "Предмет", "Учитель", "Кабинет"];
        var sheet = workbook.Worksheets.Add("Полное расписание");
        WriteHeaders(sheet, 1, headers);
        var row = 2;
        foreach (var lesson in source.OrderBy(x => x.Day).ThenBy(x => x.LessonNumber)
                     .ThenBy(x => x.SchoolClass, StringComparer.CurrentCultureIgnoreCase))
            WriteValues(sheet, row++, [DayName(lesson.Day), lesson.LessonNumber, lesson.SchoolClass,
                lesson.Group ?? "—", lesson.Subject, lesson.Teacher, lesson.Room ?? "—"]);
        sheet.SheetView.FreezeRows(1);
        if (row > 2) sheet.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
        FinishSheet(sheet, headers.Length);
    }

    private static void WriteHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var column = 1; column <= headers.Count; column++) sheet.Cell(row, column).Value = headers[column - 1];
        var range = sheet.Range(row, 1, row, headers.Count);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void WriteValues(IXLWorksheet sheet, int row, IReadOnlyList<object> values)
    {
        for (var column = 1; column <= values.Count; column++)
        {
            if (values[column - 1] is int number) sheet.Cell(row, column).Value = number;
            else sheet.Cell(row, column).Value = values[column - 1]?.ToString() ?? string.Empty;
        }
    }

    private static void FinishSheet(IXLWorksheet sheet, int columnCount)
    {
        sheet.Columns(1, columnCount).AdjustToContents(8, 35);
        sheet.RangeUsed()?.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText();
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.SetLeft(0.25).SetRight(0.25).SetTop(0.5).SetBottom(0.5);
    }

    private static string DayName(int day) => day >= 1 && day <= Days.Length ? Days[day - 1] : $"День {day}";
}
