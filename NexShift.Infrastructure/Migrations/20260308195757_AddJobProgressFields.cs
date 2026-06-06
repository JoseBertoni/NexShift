using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomatedCount",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManualCount",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MigrationPercentage",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "MigrationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomatedCount",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "ManualCount",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "MigrationPercentage",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "MigrationJobs");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "MigrationJobs");
        }
    }
}
