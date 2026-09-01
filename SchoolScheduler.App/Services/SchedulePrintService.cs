using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.App.Services;

public sealed class SchedulePrintService
{
    public bool Print(SchedulePrintData data, SchedulePrintSettings settings)
    {
        var dialog = new PrintDialog();
        dialog.PrintTicket.PageOrientation = settings.Orientation == SchedulePageOrientation.Landscape
            ? PageOrientation.Landscape : PageOrientation.Portrait;
        dialog.PrintTicket.PageMediaSize = new(settings.PaperSize == SchedulePaperSize.A3
            ? PageMediaSizeName.ISOA3 : PageMediaSizeName.ISOA4);
        if (dialog.ShowDialog() != true) return false;
        var document = BuildDocument(data, settings);
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.ColumnWidth = dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,
            $"Расписание - {data.SchoolName}");
        return true;
    }

    internal static FlowDocument BuildDocument(SchedulePrintData data, SchedulePrintSettings settings)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Arial"), FontSize = 10,
            PagePadding = new Thickness(36), ColumnGap = 0
        };
        var rows = data.Rows.Where(x => settings.Classes is null || settings.Classes.Count == 0 || settings.Classes.Contains(x.SchoolClass));
        var first = true;
        foreach (var group in rows.GroupBy(x => x.SchoolClass).OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var section = new Section { BreakPageBefore = !first };
            first = false;
            section.Blocks.Add(new Paragraph(new Run(data.SchoolName))
                { FontSize = 18, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new(0, 0, 0, 2) });
            var subtitle = $"Расписание занятий - {data.AcademicYear} - класс {group.Key}";
            if (!string.IsNullOrWhiteSpace(settings.Shift)) subtitle += $" - {settings.Shift}";
            section.Blocks.Add(new Paragraph(new Run(subtitle))
                { FontSize = 13, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, Margin = new(0, 0, 0, 14) });
            section.Blocks.Add(BuildTable(group.ToList(), settings));
            section.Blocks.Add(new Paragraph(new Run($"Создано: {data.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}"))
                { FontSize = 8, Foreground = Brushes.Gray, Margin = new(0, 10, 0, 0) });
            document.Blocks.Add(section);
        }
        if (first) document.Blocks.Add(new Paragraph(new Run("Для выбранных параметров занятия не найдены.")));
        return document;
    }

    private static Table BuildTable(IReadOnlyList<ScheduleExportRow> rows, SchedulePrintSettings settings)
    {
        var table = new Table { CellSpacing = 0 };
        var days = rows.Select(x => x.Day).Distinct().Order().ToList();
        table.Columns.Add(new TableColumn { Width = new GridLength(48) });
        foreach (var _ in days) table.Columns.Add(new TableColumn());
        var body = new TableRowGroup();
        table.RowGroups.Add(body);
        var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(31, 78, 120)) };
        header.Cells.Add(Cell("Урок", true, Brushes.White));
        foreach (var day in days) header.Cells.Add(Cell(DayName(day), true, Brushes.White));
        body.Rows.Add(header);
        foreach (var lesson in rows.Select(x => x.LessonNumber).Distinct().Order())
        {
            var row = new TableRow();
            row.Cells.Add(Cell(lesson.ToString(), true));
            foreach (var day in days)
                row.Cells.Add(Cell(string.Join("\n", rows.Where(x => x.Day == day && x.LessonNumber == lesson)
                    .Select(x => Describe(x, settings))), false));
            body.Rows.Add(row);
        }
        return table;
    }

    private static TableCell Cell(string text, bool bold, Brush? foreground = null) => new(new Paragraph(new Run(text))
    {
        TextAlignment = TextAlignment.Center, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Foreground = foreground ?? Brushes.Black, Margin = new Thickness(3)
    }) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5), Padding = new Thickness(3) };

    private static string Describe(ScheduleExportRow row, SchedulePrintSettings settings)
    {
        var parts = new List<string> { row.Subject };
        if (!string.IsNullOrWhiteSpace(row.Group)) parts.Add(row.Group!);
        if (settings.ShowTeachers) parts.Add(row.Teacher);
        if (settings.ShowRooms && !string.IsNullOrWhiteSpace(row.Room)) parts.Add($"каб. {row.Room}");
        return string.Join("\n", parts);
    }

    private static string DayName(int day) => day is >= 1 and <= 7
        ? new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }[day - 1] : $"День {day}";
}
