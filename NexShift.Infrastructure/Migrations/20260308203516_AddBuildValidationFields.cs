using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildValidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildErrorCount",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BuildResultJson",
                table: "MigrationJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BuildSuccess",
                table: "MigrationJobs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuildWarningCount",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildErrorCount",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "BuildResultJson",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "BuildSuccess",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "BuildWarningCount",
                table: "MigrationJobs");
        }
    }
}
