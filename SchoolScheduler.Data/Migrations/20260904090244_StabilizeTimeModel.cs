using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolScheduler.Data.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeTimeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LessonNumber",
                table: "TeacherAvailabilities",
                newName: "LessonPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonNumber",
                table: "TeacherAvailabilities",
                newName: "IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonPeriodId");

            migrationBuilder.RenameColumn(
                name: "LessonNumber",
                table: "RoomAvailabilities",
                newName: "LessonPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_RoomAvailabilities_RoomId_DayOfWeek_LessonNumber",
                table: "RoomAvailabilities",
                newName: "IX_RoomAvailabilities_RoomId_DayOfWeek_LessonPeriodId");

            // Legacy availability had no shift dimension. Preserve it by mapping each lesson
            // number to the first matching configured shift; users can then refine it in the UI.
            migrationBuilder.Sql("""
                UPDATE TeacherAvailabilities
                SET LessonPeriodId = (SELECT Id FROM LessonPeriods
                    WHERE Number = TeacherAvailabilities.LessonPeriodId ORDER BY ShiftId LIMIT 1)
                WHERE EXISTS (SELECT 1 FROM LessonPeriods WHERE Number = TeacherAvailabilities.LessonPeriodId);
                """);
            migrationBuilder.Sql("""
                UPDATE RoomAvailabilities
                SET LessonPeriodId = (SELECT Id FROM LessonPeriods
                    WHERE Number = RoomAvailabilities.LessonPeriodId ORDER BY ShiftId LIMIT 1)
                WHERE EXISTS (SELECT 1 FROM LessonPeriods WHERE Number = RoomAvailabilities.LessonPeriodId);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailabilities_LessonPeriodId",
                table: "TeacherAvailabilities",
                column: "LessonPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAvailabilities_LessonPeriodId",
                table: "RoomAvailabilities",
                column: "LessonPeriodId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_RoomAvailabilities_LessonPeriods_LessonPeriodId",
                table: "RoomAvailabilities",
                column: "LessonPeriodId",
                principalTable: "LessonPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAvailabilities_LessonPeriods_LessonPeriodId",
                table: "TeacherAvailabilities",
                column: "LessonPeriodId",
                principalTable: "LessonPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LessonPeriods_Shifts_ShiftId",
                table: "LessonPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomAvailabilities_LessonPeriods_LessonPeriodId",
                table: "RoomAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAvailabilities_LessonPeriods_LessonPeriodId",
                table: "TeacherAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TeacherAvailabilities_LessonPeriodId",
                table: "TeacherAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_RoomAvailabilities_LessonPeriodId",
                table: "RoomAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_LessonPeriods_ShiftId_Number",
                table: "LessonPeriods");

            migrationBuilder.RenameColumn(
                name: "LessonPeriodId",
                table: "TeacherAvailabilities",
                newName: "LessonNumber");

            migrationBuilder.RenameIndex(
                name: "IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonPeriodId",
                table: "TeacherAvailabilities",
                newName: "IX_TeacherAvailabilities_TeacherId_DayOfWeek_LessonNumber");

            migrationBuilder.RenameColumn(
                name: "LessonPeriodId",
                table: "RoomAvailabilities",
                newName: "LessonNumber");

            migrationBuilder.RenameIndex(
                name: "IX_RoomAvailabilities_RoomId_DayOfWeek_LessonPeriodId",
                table: "RoomAvailabilities",
                newName: "IX_RoomAvailabilities_RoomId_DayOfWeek_LessonNumber");
        }
    }
}
