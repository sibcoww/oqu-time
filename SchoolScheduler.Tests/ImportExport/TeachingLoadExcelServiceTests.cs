using ClosedXML.Excel;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.Tests.ImportExport;

public sealed class TeachingLoadExcelServiceTests
{
    [Fact]
    public void Template_CanBeFilledAndImportedWithFractionalHours()
    {
        var path = Path.Combine(Path.GetTempPath(), $"load-template-{Guid.NewGuid():N}.xlsx");
        try
        {
            var service = new TeachingLoadExcelService();
            service.CreateTemplate(path, new(["Иванова А.А."], ["Математика"], ["7Б"], [], ["12"]));
            using (var workbook = new XLWorkbook(path))
            {
                var sheet = workbook.Worksheet(TeachingLoadExcelService.SheetName);
                sheet.Cell("A2").Value = "Иванова А.А."; sheet.Cell("B2").Value = "Математика";
                sheet.Cell("C2").Value = "7Б"; sheet.Cell("E2").Value = 0.25m;
                sheet.Cell("F2").Value = "12"; sheet.Cell("G2").Value = "Да";
                sheet.Cell("H2").Value = "Раз в четыре недели"; workbook.Save();
            }

            var result = service.Import(path);
            Assert.Empty(result.Errors);
            var row = Assert.Single(result.Rows);
            Assert.Equal(0.25m, row.HoursPerWeek);
            Assert.True(row.AllowZeroLesson);
            Assert.Equal("12", row.Room);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Import_RejectsWorkbookWithChangedHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"invalid-template-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            { var sheet = workbook.AddWorksheet(TeachingLoadExcelService.SheetName); sheet.Cell("A1").Value = "ФИО"; workbook.SaveAs(path); }
            var result = new TeachingLoadExcelService().Import(path);
            Assert.Empty(result.Rows); Assert.Contains(result.Errors, x => x.Contains("ожидается заголовок"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
