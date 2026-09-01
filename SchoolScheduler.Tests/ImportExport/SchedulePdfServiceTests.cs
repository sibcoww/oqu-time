using PdfSharp.Pdf.IO;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.Tests.ImportExport;

public sealed class SchedulePdfServiceTests
{
    [Fact]
    public void Export_CreatesPrintablePagePerSelectedClassWithMetadata()
    {
        var requestedPath = Environment.GetEnvironmentVariable("SCHEDULE_PDF_QA_PATH");
        var path = requestedPath ?? Path.Combine(Path.GetTempPath(), $"schedule-{Guid.NewGuid():N}.pdf");
        try
        {
            var rows = SampleRows();
            var data = new SchedulePrintData("Школа №15", "2026–2027",
                new DateTimeOffset(2026, 9, 1, 10, 30, 0, TimeSpan.Zero), rows);
            var settings = new SchedulePrintSettings(SchedulePaperSize.A4,
                SchedulePageOrientation.Landscape, true, true, new HashSet<string> { "7Б" }, "Первая смена");

            new SchedulePdfService().Export(path, data, settings);

            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Assert.Single(document.Pages);
            Assert.True(document.Pages[0].Width.Point > document.Pages[0].Height.Point);
            Assert.Equal("Расписание - Школа №15", document.Info.Title);
            Assert.Equal("SchoolScheduler", document.Info.Author);
        }
        finally { if (requestedPath is null && File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Export_CreatesA3PortraitAndOnePageForEachClass()
    {
        var path = Path.Combine(Path.GetTempPath(), $"schedule-a3-{Guid.NewGuid():N}.pdf");
        try
        {
            new SchedulePdfService().Export(path, new("Школа", "2026–2027", DateTimeOffset.Now, SampleRows()),
                new(SchedulePaperSize.A3, SchedulePageOrientation.Portrait, false, false));
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            Assert.Equal(2, document.PageCount);
            Assert.True(document.Pages[0].Height.Point > document.Pages[0].Width.Point);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static ScheduleExportRow[] SampleRows() =>
    [
        new(1, 1, "7Б", null, "Математика", "Иванова А.А.", "12", "Первая смена"),
        new(1, 2, "7Б", "1 группа", "Английский язык", "Петрова О.В.", "24", "Первая смена"),
        new(2, 1, "7Б", null, "История Казахстана", "Сидоров И.И.", "18", "Первая смена"),
        new(1, 1, "8А", null, "Физика", "Ким Н.Н.", "21", "Первая смена")
    ];
}
