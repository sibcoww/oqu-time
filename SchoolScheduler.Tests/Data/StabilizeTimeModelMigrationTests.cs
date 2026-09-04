using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolScheduler.Data;

namespace SchoolScheduler.Tests.Data;

public sealed class StabilizeTimeModelMigrationTests
{
    [Fact]
    public void LegacyAvailability_ExpandsAcrossShifts_DropsUnknownLessons_AndHasNoOrphans()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stabilize-time-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;
            using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            migrator.Migrate("20260901033912_ExpandTeachingLoads");
            SeedLegacyData(db);

            migrator.Migrate("20260904090244_StabilizeTimeModel");

            Assert.Equal(12, Scalar(db, "SELECT COUNT(*) FROM TeacherAvailabilities"));
            Assert.Equal(12, Scalar(db, "SELECT COUNT(*) FROM RoomAvailabilities"));
            Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM TeacherAvailabilities a LEFT JOIN LessonPeriods p ON p.Id=a.LessonPeriodId WHERE p.Id IS NULL"));
            Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM RoomAvailabilities a LEFT JOIN LessonPeriods p ON p.Id=a.LessonPeriodId WHERE p.Id IS NULL"));
            Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM (SELECT TeacherId,DayOfWeek,LessonPeriodId,COUNT(*) c FROM TeacherAvailabilities GROUP BY TeacherId,DayOfWeek,LessonPeriodId HAVING c>1)"));
            Assert.Equal(2, Scalar(db, "SELECT COUNT(*) FROM TeacherAvailabilities a JOIN LessonPeriods p ON p.Id=a.LessonPeriodId WHERE p.Number=2 AND a.IsAvailable=0"));

            migrator.Migrate("20260901033912_ExpandTeachingLoads");
            Assert.Equal(6, Scalar(db, "SELECT COUNT(*) FROM TeacherAvailabilities"));
            Assert.Equal(6, Scalar(db, "SELECT COUNT(DISTINCT LessonNumber) FROM TeacherAvailabilities WHERE LessonNumber BETWEEN 1 AND 6"));
            db.Database.CloseConnection();
            db.Dispose();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void SeedLegacyData(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            INSERT INTO Schools (Name,DaysPerWeek,Region,UseRegionalNorms) VALUES ('Legacy school',6,'KZ',0);
            INSERT INTO Shifts (Id,Name) VALUES (1,'Shift 1'),(2,'Shift 2');
            INSERT INTO Teachers (Id,FullName,IsActive) VALUES (1,'Teacher',1);
            INSERT INTO Rooms (Id,Name,IsActive,Type) VALUES (1,'Room',1,0);
            INSERT INTO LessonPeriods (ShiftId,Number,StartTime,EndTime) VALUES
             (1,1,'08:00:00','08:45:00'),(1,2,'08:50:00','09:35:00'),(1,3,'09:40:00','10:25:00'),
             (1,4,'10:30:00','11:15:00'),(1,5,'11:20:00','12:05:00'),(1,6,'12:10:00','12:55:00'),
             (2,1,'14:00:00','14:45:00'),(2,2,'14:50:00','15:35:00'),(2,3,'15:40:00','16:25:00'),
             (2,4,'16:30:00','17:15:00'),(2,5,'17:20:00','18:05:00'),(2,6,'18:10:00','18:55:00');
            INSERT INTO TeacherAvailabilities (TeacherId,DayOfWeek,LessonNumber,IsAvailable) VALUES
             (1,1,0,1),(1,1,1,1),(1,1,2,0),(1,1,3,1),(1,1,4,1),(1,1,5,1),(1,1,6,1),(1,1,7,0),(1,1,8,1);
            INSERT INTO RoomAvailabilities (RoomId,DayOfWeek,LessonNumber,IsAvailable) VALUES
             (1,1,0,0),(1,1,1,1),(1,1,2,0),(1,1,3,1),(1,1,4,1),(1,1,5,1),(1,1,6,1),(1,1,7,1),(1,1,8,0);
            """);
    }

    private static long Scalar(AppDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
