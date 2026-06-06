using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransformationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Replacement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NeedsAI = table.Column<bool>(type: "boolean", nullable: false),
                    IsRegex = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationRules", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TransformationRules",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "IsRegex", "NeedsAI", "Pattern", "Priority", "Replacement", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Mvc → Microsoft.AspNetCore.Mvc", true, false, false, "using System.Web.Mvc;", 10, "using Microsoft.AspNetCore.Mvc;", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Http → Microsoft.AspNetCore.Mvc", true, false, false, "using System.Web.Http;", 10, "using Microsoft.AspNetCore.Mvc;", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Optimization → removed", true, false, false, "using System.Web.Optimization;", 10, "// REMOVED: System.Web.Optimization - not available in .NET modern", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Routing → removed", true, false, false, "using System.Web.Routing;", 10, "// REMOVED: System.Web.Routing - use ASP.NET Core routing", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ApiController → ControllerBase", true, false, false, ": ApiController", 20, ": ControllerBase", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MVC Controller kept as-is", true, false, false, ": Controller", 20, ": Controller", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Http.HttpGet → HttpGet", true, false, false, "[System.Web.Http.HttpGet]", 20, "[HttpGet]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Http.HttpPost → HttpPost", true, false, false, "[System.Web.Http.HttpPost]", 20, "[HttpPost]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Http.HttpPut → HttpPut", true, false, false, "[System.Web.Http.HttpPut]", 20, "[HttpPut]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Web.Http.HttpDelete → HttpDelete", true, false, false, "[System.Web.Http.HttpDelete]", 20, "[HttpDelete]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "RoutePrefix → Route", true, false, false, "RoutePrefix(", 20, "Route(", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "WebConfigurationManager → IConfiguration comment", true, false, false, "WebConfigurationManager.AppSettings", 30, "// TODO: NEXSHIFT - Inject IConfiguration and use _configuration[\"key\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ConfigurationManager → IConfiguration comment", true, false, false, "ConfigurationManager.AppSettings", 30, "// TODO: NEXSHIFT - Inject IConfiguration and use _configuration[\"key\"]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HttpResponseMessage → IActionResult", true, false, false, "HttpResponseMessage", 30, "IActionResult", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "IHttpActionResult → IActionResult", true, false, false, "IHttpActionResult", 30, "IActionResult", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Request.CreateResponse OK → Ok()", true, false, false, "Request.CreateResponse(HttpStatusCode.OK,", 30, "Ok(", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Request.CreateResponse BadRequest → BadRequest()", true, false, false, "Request.CreateResponse(HttpStatusCode.BadRequest,", 30, "BadRequest(", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Request.CreateResponse NotFound → NotFound()", true, false, false, "Request.CreateResponse(HttpStatusCode.NotFound)", 30, "NotFound()", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HttpContext.Current → needs IHttpContextAccessor DI", true, false, true, "HttpContext.Current", 50, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GlobalConfiguration → needs Program.cs refactor", true, false, true, "GlobalConfiguration.Configure", 50, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Drawing → needs SkiaSharp or ImageSharp migration", true, false, true, "System.Drawing", 50, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Microsoft.Owin → needs ASP.NET Core middleware rewrite", true, false, true, "Microsoft.Owin", 50, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRules_Pattern",
                table: "TransformationRules",
                column: "Pattern");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRules_Priority",
                table: "TransformationRules",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransformationRules");
        }
    }
}
