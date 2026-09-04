using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolScheduler.Data.Migrations;

public partial class StabilizeTimeModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE TeacherAvailabilities_New (
                Id INTEGER NOT NULL CONSTRAINT PK_TeacherAvailabilities PRIMARY KEY AUTOINCREMENT,
                TeacherId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                LessonPeriodId INTEGER NOT NULL,
                IsAvailable INTEGER NOT NULL,
                CONSTRAINT FK_TeacherAvailabilities_Teachers_TeacherId FOREIGN KEY (TeacherId) REFERENCES Teachers (Id) ON DELETE CASCADE,
                CONSTRAINT FK_TeacherAvailabilities_LessonPeriods_LessonPeriodId FOREIGN KEY (LessonPeriodId) REFERENCES LessonPeriods (Id) ON DELETE CASCADE
            );
            INSERT INTO TeacherAvailabilities_New (TeacherId, DayOfWeek, LessonPeriodId, IsAvailable)
            SELECT DISTINCT legacy.TeacherId, legacy.DayOfWeek, period.Id, legacy.IsAvailable
            FROM TeacherAvailabilities AS legacy
            INNER JOIN LessonPeriods AS period ON period.Number = legacy.LessonNumber;
            DROP TABLE TeacherAvailabilities;
            ALTER TABLE TeacherAvailabilities_New RENAME TO TeacherAvailabilities;
            CREATE INDEX IX_TeacherAvailabilities_LessonPeriodId ON TeacherAvailabilities (LessonPeriodId);
            CREATE UNIQUE INDEX IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonPeriodId
                ON TeacherAvailabilities (TeacherId, DayOfWeek, LessonPeriodId);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE RoomAvailabilities_New (
                Id INTEGER NOT NULL CONSTRAINT PK_RoomAvailabilities PRIMARY KEY AUTOINCREMENT,
                RoomId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                LessonPeriodId INTEGER NOT NULL,
                IsAvailable INTEGER NOT NULL,
                CONSTRAINT FK_RoomAvailabilities_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms (Id) ON DELETE CASCADE,
                CONSTRAINT FK_RoomAvailabilities_LessonPeriods_LessonPeriodId FOREIGN KEY (LessonPeriodId) REFERENCES LessonPeriods (Id) ON DELETE CASCADE
            );
            INSERT INTO RoomAvailabilities_New (RoomId, DayOfWeek, LessonPeriodId, IsAvailable)
            SELECT DISTINCT legacy.RoomId, legacy.DayOfWeek, period.Id, legacy.IsAvailable
            FROM RoomAvailabilities AS legacy
            INNER JOIN LessonPeriods AS period ON period.Number = legacy.LessonNumber;
            DROP TABLE RoomAvailabilities;
            ALTER TABLE RoomAvailabilities_New RENAME TO RoomAvailabilities;
            CREATE INDEX IX_RoomAvailabilities_LessonPeriodId ON RoomAvailabilities (LessonPeriodId);
            CREATE UNIQUE INDEX IX_RoomAvailabilities_RoomId_DayOfWeek_LessonPeriodId
                ON RoomAvailabilities (RoomId, DayOfWeek, LessonPeriodId);
            """);

        migrationBuilder.CreateIndex(
            name: "IX_LessonPeriods_ShiftId_Number",
            table: "LessonPeriods",
            columns: new[] { "ShiftId", "Number" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_LessonPeriods_Shifts_ShiftId",
            table: "LessonPeriods",
            column: "ShiftId",
            principalTable: "Shifts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_LessonPeriods_Shifts_ShiftId", table: "LessonPeriods");
        migrationBuilder.DropIndex(name: "IX_LessonPeriods_ShiftId_Number", table: "LessonPeriods");

        migrationBuilder.Sql("""
            CREATE TABLE TeacherAvailabilities_Legacy (
                Id INTEGER NOT NULL CONSTRAINT PK_TeacherAvailabilities PRIMARY KEY AUTOINCREMENT,
                TeacherId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                LessonNumber INTEGER NOT NULL,
                IsAvailable INTEGER NOT NULL,
                CONSTRAINT FK_TeacherAvailabilities_Teachers_TeacherId FOREIGN KEY (TeacherId) REFERENCES Teachers (Id) ON DELETE CASCADE
            );
            INSERT INTO TeacherAvailabilities_Legacy (TeacherId, DayOfWeek, LessonNumber, IsAvailable)
            SELECT current.TeacherId, current.DayOfWeek, period.Number, MIN(current.IsAvailable)
            FROM TeacherAvailabilities AS current
            INNER JOIN LessonPeriods AS period ON period.Id = current.LessonPeriodId
            GROUP BY current.TeacherId, current.DayOfWeek, period.Number;
            DROP TABLE TeacherAvailabilities;
            ALTER TABLE TeacherAvailabilities_Legacy RENAME TO TeacherAvailabilities;
            CREATE UNIQUE INDEX IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonNumber
                ON TeacherAvailabilities (TeacherId, DayOfWeek, LessonNumber);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE RoomAvailabilities_Legacy (
                Id INTEGER NOT NULL CONSTRAINT PK_RoomAvailabilities PRIMARY KEY AUTOINCREMENT,
                RoomId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                LessonNumber INTEGER NOT NULL,
                IsAvailable INTEGER NOT NULL,
                CONSTRAINT FK_RoomAvailabilities_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES Rooms (Id) ON DELETE CASCADE
            );
            INSERT INTO RoomAvailabilities_Legacy (RoomId, DayOfWeek, LessonNumber, IsAvailable)
            SELECT current.RoomId, current.DayOfWeek, period.Number, MIN(current.IsAvailable)
            FROM RoomAvailabilities AS current
            INNER JOIN LessonPeriods AS period ON period.Id = current.LessonPeriodId
            GROUP BY current.RoomId, current.DayOfWeek, period.Number;
            DROP TABLE RoomAvailabilities;
            ALTER TABLE RoomAvailabilities_Legacy RENAME TO RoomAvailabilities;
            CREATE UNIQUE INDEX IX_RoomAvailabilities_RoomId_DayOfWeek_LessonNumber
                ON RoomAvailabilities (RoomId, DayOfWeek, LessonNumber);
            """);
    }
}
