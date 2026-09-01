using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace SchoolScheduler.ImportExport;

public enum SchedulePaperSize { A4, A3 }
public enum SchedulePageOrientation { Portrait, Landscape }

public sealed record SchedulePrintSettings(SchedulePaperSize PaperSize,
    SchedulePageOrientation Orientation, bool ShowTeachers, bool ShowRooms,
    IReadOnlySet<string>? Classes = null, string? Shift = null);

public sealed record SchedulePrintData(string SchoolName, string AcademicYear,
    DateTimeOffset CreatedAt, IReadOnlyCollection<ScheduleExportRow> Rows);

public sealed class SchedulePdfService
{
    public void Export(string path, SchedulePrintData data, SchedulePrintSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var document = BuildDocument(data, settings);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = $"Расписание - {Safe(data.SchoolName)}";
        renderer.PdfDocument.Info.Author = "SchoolScheduler";
        renderer.PdfDocument.Save(path);
    }

    internal static Document BuildDocument(SchedulePrintData data, SchedulePrintSettings settings)
    {
        var document = new Document();
        document.Info.Title = $"Расписание - {Safe(data.SchoolName)}";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 8;
        var rows = data.Rows.Where(x => settings.Classes is null || settings.Classes.Count == 0 || settings.Classes.Contains(x.SchoolClass)).ToList();
        foreach (var classGroup in rows.GroupBy(x => x.SchoolClass).OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
            AddClassSection(document, data, settings, classGroup.Key, classGroup.ToList());
        if (document.Sections.Count == 0) AddEmptySection(document, data, settings);
        return document;
    }

    private static void AddClassSection(Document document, SchedulePrintData data,
        SchedulePrintSettings settings, string className, IReadOnlyList<ScheduleExportRow> rows)
    {
        var section = document.AddSection();
        ConfigurePage(section, settings);
        AddHeader(section, data, settings, className);
        var days = rows.Select(x => x.Day).Distinct().Order().ToList();
        var lessons = rows.Select(x => x.LessonNumber).Distinct().Order().ToList();
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.Rows.LeftIndent = 0;
        table.AddColumn(Unit.FromCentimeter(1.05));
        var availableWidth = PageWidth(settings) - 2.4;
        foreach (var _ in days) table.AddColumn(Unit.FromCentimeter(availableWidth / Math.Max(1, days.Count)));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.Parse("#1F4E78");
        SetCell(header.Cells[0], "Урок", true, Colors.White);
        for (var index = 0; index < days.Count; index++) SetCell(header.Cells[index + 1], DayName(days[index]), true, Colors.White);
        foreach (var lessonNumber in lessons)
        {
            var row = table.AddRow();
            row.TopPadding = Unit.FromMillimeter(2);
            row.BottomPadding = Unit.FromMillimeter(2);
            SetCell(row.Cells[0], lessonNumber.ToString(), true);
            for (var index = 0; index < days.Count; index++)
            {
                var items = rows.Where(x => x.Day == days[index] && x.LessonNumber == lessonNumber)
                    .Select(x => Describe(x, settings));
                SetCell(row.Cells[index + 1], string.Join("\n", items), false);
            }
        }
        AddFooter(section, data);
    }

    private static void AddEmptySection(Document document, SchedulePrintData data, SchedulePrintSettings settings)
    {
        var section = document.AddSection();
        ConfigurePage(section, settings);
        AddHeader(section, data, settings, "");
        section.AddParagraph("Для выбранных параметров занятия не найдены.");
        AddFooter(section, data);
    }

    private static void AddHeader(Section section, SchedulePrintData data, SchedulePrintSettings settings, string className)
    {
        var title = section.AddParagraph();
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Size = 15;
        title.Format.Font.Bold = true;
        title.AddText(Safe(data.SchoolName));
        var subtitle = section.AddParagraph();
        subtitle.Format.Alignment = ParagraphAlignment.Center;
        subtitle.Format.Font.Size = 11;
        subtitle.Format.SpaceAfter = Unit.FromMillimeter(5);
        subtitle.AddText($"Расписание занятий - {Safe(data.AcademicYear)}");
        if (!string.IsNullOrWhiteSpace(className)) subtitle.AddText($" - класс {Safe(className)}");
        if (!string.IsNullOrWhiteSpace(settings.Shift)) subtitle.AddText($" - {Safe(settings.Shift)}");
    }

    private static void AddFooter(Section section, SchedulePrintData data)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText($"Создано: {data.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}  |  Страница ");
        footer.AddPageField();
        footer.AddText(" из ");
        footer.AddNumPagesField();
    }

    private static void ConfigurePage(Section section, SchedulePrintSettings settings)
    {
        section.PageSetup.PageFormat = settings.PaperSize == SchedulePaperSize.A3 ? PageFormat.A3 : PageFormat.A4;
        section.PageSetup.Orientation = settings.Orientation == SchedulePageOrientation.Landscape
            ? Orientation.Landscape : Orientation.Portrait;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
    }

    private static void SetCell(Cell cell, string text, bool bold, Color? color = null)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        var paragraph = cell.AddParagraph(text);
        paragraph.Format.Alignment = ParagraphAlignment.Center;
        paragraph.Format.Font.Bold = bold;
        if (color is not null) paragraph.Format.Font.Color = color.Value;
    }

    private static string Describe(ScheduleExportRow row, SchedulePrintSettings settings)
    {
        var parts = new List<string> { Safe(row.Subject) };
        if (!string.IsNullOrWhiteSpace(row.Group)) parts.Add(Safe(row.Group!));
        if (settings.ShowTeachers) parts.Add(Safe(row.Teacher));
        if (settings.ShowRooms && !string.IsNullOrWhiteSpace(row.Room)) parts.Add($"каб. {Safe(row.Room!)}");
        return string.Join("\n", parts);
    }

    private static double PageWidth(SchedulePrintSettings settings) => (settings.PaperSize, settings.Orientation) switch
    {
        (SchedulePaperSize.A3, SchedulePageOrientation.Landscape) => 39.6,
        (SchedulePaperSize.A3, _) => 27.3,
        (SchedulePaperSize.A4, SchedulePageOrientation.Landscape) => 27.3,
        _ => 18.6
    };

    private static string DayName(int day) => day is >= 1 and <= 7
        ? new[] { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" }[day - 1]
        : $"День {day}";

    private static string Safe(string value) => value.Replace('\u2010', '-').Replace('\u2011', '-')
        .Replace('\u2012', '-').Replace('\u2013', '-').Replace('\u2014', '-');
}
