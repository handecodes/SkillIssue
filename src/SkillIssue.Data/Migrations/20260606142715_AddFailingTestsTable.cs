using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillIssue.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFailingTestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create new table first so the data migration can insert into it.
            migrationBuilder.CreateTable(
                name: "FailingTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BugId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TestName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailingTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailingTests_Bugs_BugId",
                        column: x => x.BugId,
                        principalTable: "Bugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailingTests_BugId",
                table: "FailingTests",
                column: "BugId");

            // 2. Migrate existing CSV data. All current rows have a single test name
            //    (no real comma-separated values), so treating the whole string as one
            //    test name is correct. Multi-value rows would need a separate script.
            migrationBuilder.Sql(
                """
                INSERT INTO "FailingTests" ("BugId", "Order", "TestName")
                SELECT "Id", 1, "FailingTests"
                FROM "Bugs"
                WHERE "FailingTests" != '';
                """);

            // 3. Drop the now-redundant column. On SQLite EF rebuilds the Bugs table,
            //    which is safe because FailingTests rows were inserted in step 2.
            migrationBuilder.DropColumn(
                name: "FailingTests",
                table: "Bugs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailingTests");

            migrationBuilder.AddColumn<string>(
                name: "FailingTests",
                table: "Bugs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
