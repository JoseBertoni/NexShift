using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNugetPackagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NugetPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LatestVersion = table.Column<string>(type: "text", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedReplacement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastChecked = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsManuallyAdded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NugetPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NugetPackages_Name",
                table: "NugetPackages",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NugetPackages");
        }
    }
}
