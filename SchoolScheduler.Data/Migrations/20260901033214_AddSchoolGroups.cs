using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolScheduler.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "TeachingLoads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ClassId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolGroups_SchoolClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolGroups_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolGroups_ClassId_Name",
                table: "SchoolGroups",
                columns: new[] { "ClassId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolGroups_SubjectId",
                table: "SchoolGroups",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolGroups");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "TeachingLoads");
        }
    }
}
