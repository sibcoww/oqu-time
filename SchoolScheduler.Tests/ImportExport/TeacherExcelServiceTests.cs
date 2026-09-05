using ClosedXML.Excel;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.Tests.ImportExport;

public sealed class TeacherExcelServiceTests
{
    [Fact]
    public void Import_ReadsSampleStyleSheetsAndRemovesDuplicates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"teachers-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var original = workbook.AddWorksheet("Как в Word");
                original.Cell("A1").Value = "Нагрузка учителей";
                original.Cell("B2").Value = "Мұғалімнің аты-жөні\nФамилия имя отчество учителя";
                original.Cell("B3").Value = "  Иванова   Анна Петровна ";
                var expanded = workbook.AddWorksheet("По классам");
                expanded.Cell("B1").Value = "ФИО учителя";
                expanded.Cell("B2").Value = "Иванова Анна Петровна";
                expanded.Cell("B3").Value = "Сериков Болат Ерланович";
                workbook.SaveAs(path);
            }
            var result = new TeacherExcelService().Import(path);
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Names.Count);
            Assert.Contains("Иванова Анна Петровна", result.Names);
            Assert.Contains("Сериков Болат Ерланович", result.Names);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Import_ReturnsClearErrorWhenTeacherColumnIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"teachers-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                workbook.AddWorksheet("Данные").Cell("A1").Value = "Предмет";
                workbook.SaveAs(path);
            }
            var result = new TeacherExcelService().Import(path);
            Assert.Empty(result.Names);
            Assert.Single(result.Errors);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
