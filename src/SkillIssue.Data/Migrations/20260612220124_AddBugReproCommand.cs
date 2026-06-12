using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillIssue.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBugReproCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReproCommand",
                table: "Bugs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReproCommand",
                table: "Bugs");
        }
    }
}
