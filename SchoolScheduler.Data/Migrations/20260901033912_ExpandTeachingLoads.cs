using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolScheduler.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTeachingLoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "HoursPerWeek",
                table: "TeachingLoads",
                type: "TEXT",
                precision: 6,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "AllowZeroLesson",
                table: "TeachingLoads",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "TeachingLoads",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "TeachingLoads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingLoads_ClassId",
                table: "TeachingLoads",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingLoads_GroupId",
                table: "TeachingLoads",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingLoads_RoomId",
                table: "TeachingLoads",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingLoads_SubjectId",
                table: "TeachingLoads",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingLoads_TeacherId",
                table: "TeachingLoads",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeachingLoads_Rooms_RoomId",
                table: "TeachingLoads",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TeachingLoads_SchoolClasses_ClassId",
                table: "TeachingLoads",
                column: "ClassId",
                principalTable: "SchoolClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeachingLoads_SchoolGroups_GroupId",
                table: "TeachingLoads",
                column: "GroupId",
                principalTable: "SchoolGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TeachingLoads_Subjects_SubjectId",
                table: "TeachingLoads",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeachingLoads_Teachers_TeacherId",
                table: "TeachingLoads",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeachingLoads_Rooms_RoomId",
                table: "TeachingLoads");

            migrationBuilder.DropForeignKey(
                name: "FK_TeachingLoads_SchoolClasses_ClassId",
                table: "TeachingLoads");

            migrationBuilder.DropForeignKey(
                name: "FK_TeachingLoads_SchoolGroups_GroupId",
                table: "TeachingLoads");

            migrationBuilder.DropForeignKey(
                name: "FK_TeachingLoads_Subjects_SubjectId",
                table: "TeachingLoads");

            migrationBuilder.DropForeignKey(
                name: "FK_TeachingLoads_Teachers_TeacherId",
                table: "TeachingLoads");

            migrationBuilder.DropIndex(
                name: "IX_TeachingLoads_ClassId",
                table: "TeachingLoads");

            migrationBuilder.DropIndex(
                name: "IX_TeachingLoads_GroupId",
                table: "TeachingLoads");

            migrationBuilder.DropIndex(
                name: "IX_TeachingLoads_RoomId",
                table: "TeachingLoads");

            migrationBuilder.DropIndex(
                name: "IX_TeachingLoads_SubjectId",
                table: "TeachingLoads");

            migrationBuilder.DropIndex(
                name: "IX_TeachingLoads_TeacherId",
                table: "TeachingLoads");

            migrationBuilder.DropColumn(
                name: "AllowZeroLesson",
                table: "TeachingLoads");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "TeachingLoads");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "TeachingLoads");

            migrationBuilder.AlterColumn<int>(
                name: "HoursPerWeek",
                table: "TeachingLoads",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldPrecision: 6,
                oldScale: 2);
        }
    }
}
