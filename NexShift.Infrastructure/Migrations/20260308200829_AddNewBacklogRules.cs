using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewBacklogRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "BacklogRules",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "IsActive", "Pattern", "Reason", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 23, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BinaryFormatter está obsoleto y deshabilitado por defecto en .NET moderno por riesgo de ejecución remota de código.", true, "BinaryFormatter", "Migrar a System.Text.Json, XmlSerializer o Google Protocol Buffers. Ver: aka.ms/binaryformatter", "BinaryFormatter (vulnerabilidad de seguridad)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 24, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Thread.Abort lanza ThreadAbortException, que fue eliminada en .NET 5+.", true, "Thread.Abort", "Reemplazar con CancellationToken para cancelación cooperativa. Es un cambio de patrón de diseño.", "Thread.Abort eliminado en .NET moderno", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "La creación de AppDomains secundarios fue eliminada. Solo existe el AppDomain raíz.", true, "AppDomain.CreateDomain", "Usar AssemblyLoadContext como alternativa para aislamiento de ensamblados.", "AppDomain.CreateDomain no disponible en .NET moderno", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Runtime.Remoting no existe en .NET moderno.", true, "System.Runtime.Remoting", "Migrar a gRPC (recomendado) o WCF/CoreWCF para comunicación entre procesos.", ".NET Remoting completamente eliminado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 27, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "El namespace System.Web.Security no existe en .NET moderno.", true, "System.Web.Security", "Migrar a Microsoft.AspNetCore.Identity o ASP.NET Core Authentication. Requiere decisión arquitectural.", "System.Web.Security eliminado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System.Diagnostics.EventLog solo funciona en Windows.", true, "System.Diagnostics.EventLog", "Reemplazar con Microsoft.Extensions.Logging + Serilog/NLog para logging cross-platform.", "EventLog (solo Windows)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Server.MapPath depende de IIS y no está disponible en ASP.NET Core.", true, "Server.MapPath", "Reemplazar con IWebHostEnvironment.WebRootPath o IHostEnvironment.ContentRootPath.", "Server.MapPath (IIS-specific)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HttpWebRequest es una API legacy de networking.", true, "HttpWebRequest", "Reemplazar con HttpClient inyectado via IHttpClientFactory. Es más eficiente y testeable.", "HttpWebRequest obsoleto", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "BacklogRules",
                keyColumn: "Id",
                keyValue: 30);
        }
    }
}
