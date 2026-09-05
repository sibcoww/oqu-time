using ClosedXML.Excel;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.Tests.ImportExport;

public sealed class SchoolDataExcelServiceTests
{
    [Fact]
    public void Import_ReadsNormalizedSchoolData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"school-data-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("По классам");
                var headers = new[] { "№", "ФИО учителя", "Предмет", "Короткое название", "Класс", "Часов в неделю", "Кабинет", "Группа", "Нулевой урок" };
                for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell("B2").Value = "Иванова Анна"; sheet.Cell("C2").Value = "Математика";
                sheet.Cell("D2").Value = "Матем."; sheet.Cell("E2").Value = "7А"; sheet.Cell("F2").Value = 5;
                sheet.Cell("G2").Value = "12"; sheet.Cell("H2").Value = "Группа 1"; sheet.Cell("I2").Value = "Да";
                workbook.SaveAs(path);
            }
            var result = new SchoolDataExcelService().Import(path);
            var row = Assert.Single(result.Rows);
            Assert.Empty(result.Errors); Assert.Equal("Иванова Анна", row.Teacher);
            Assert.Equal("Математика", row.Subject); Assert.Equal("7А", row.SchoolClass);
            Assert.Equal(5, row.HoursPerWeek); Assert.Equal("12", row.Room);
            Assert.Equal("Группа 1", row.Group); Assert.True(row.AllowZeroLesson);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
