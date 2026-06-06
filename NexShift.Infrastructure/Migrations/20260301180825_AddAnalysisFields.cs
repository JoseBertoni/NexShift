using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisResultJson",
                table: "Repositories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeprecatedPackages",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsLegacy",
                table: "Repositories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalCsFiles",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPackages",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalProjects",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisResultJson",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "DeprecatedPackages",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "IsLegacy",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "TotalCsFiles",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "TotalPackages",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "TotalProjects",
                table: "Repositories");
        }
    }
}
