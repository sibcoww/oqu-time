using ClosedXML.Excel;
using SchoolScheduler.ImportExport;

namespace SchoolScheduler.Tests.ImportExport;

public sealed class ScheduleExcelServiceTests
{
    [Fact]
    public void Export_CreatesFourReadableScheduleViews()
    {
        var path = Path.Combine(Path.GetTempPath(), $"schedule-{Guid.NewGuid():N}.xlsx");
        try
        {
            ScheduleExportRow[] rows =
            [
                new(1, 1, "7Б", null, "Математика", "Иванова А.А.", "12"),
                new(2, 2, "8А", "1 группа", "Английский язык", "Петрова О.В.", "24")
            ];
            new ScheduleExcelService().Export(path, rows);
            using var workbook = new XLWorkbook(path);
            Assert.Equal(["По классам", "По учителям", "По кабинетам", "Полное расписание"],
                workbook.Worksheets.Select(x => x.Name).ToArray());
            Assert.Equal("Класс: 7Б", workbook.Worksheet("По классам").Cell("A1").GetString());
            Assert.Equal("Учитель: Иванова А.А.", workbook.Worksheet("По учителям").Cell("A1").GetString());
            Assert.Equal("Кабинет: 12", workbook.Worksheet("По кабинетам").Cell("A1").GetString());
            var full = workbook.Worksheet("Полное расписание");
            Assert.Equal("Понедельник", full.Cell("A2").GetString());
            Assert.Equal("Математика", full.Cell("E2").GetString());
            Assert.True(full.Column(5).Width >= 8);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Export_LeavesRoomlessLessonsOutOfRoomViewButKeepsThemInFullView()
    {
        var path = Path.Combine(Path.GetTempPath(), $"schedule-no-room-{Guid.NewGuid():N}.xlsx");
        try
        {
            new ScheduleExcelService().Export(path,
                [new(1, 1, "7Б", null, "Физкультура", "Сидоров И.И.", null)]);
            using var workbook = new XLWorkbook(path);
            Assert.Contains("нет занятий", workbook.Worksheet("По кабинетам").Cell("A1").GetString());
            Assert.Equal("—", workbook.Worksheet("Полное расписание").Cell("G2").GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
