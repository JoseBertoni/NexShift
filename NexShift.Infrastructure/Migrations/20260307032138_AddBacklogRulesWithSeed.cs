using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexShift.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBacklogRulesWithSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacklogRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacklogRules", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BacklogRules",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "IsActive", "Pattern", "Reason", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Este archivo usa WCF que no existe en .NET moderno.", true, "ServiceContract", "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", "WCF Service detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Este archivo usa WCF que no existe en .NET moderno.", true, "OperationContract", "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", "WCF Service detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Este archivo usa WCF que no existe en .NET moderno.", true, "System.ServiceModel", "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", "WCF Service detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Los controles Web Forms no tienen equivalente en .NET moderno.", true, "System.Web.UI", "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", "ASP.NET Web Forms detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Los controles Web Forms no tienen equivalente en .NET moderno.", true, "Page_Load", "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", "ASP.NET Web Forms detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Los controles Web Forms no tienen equivalente en .NET moderno.", true, "ViewState", "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", "ASP.NET Web Forms detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FormsAuthentication no existe en .NET moderno.", true, "FormsAuthentication", "Decisión requerida: ¿JWT + Identity, Azure AD, Auth0? El motor no puede elegir por vos.", "Autenticación legacy detectada", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "WindowsIdentity requiere decisión arquitectural.", true, "WindowsIdentity", "Decisión requerida: ¿JWT + Identity, Azure AD, Auth0? El motor no puede elegir por vos.", "Autenticación legacy detectada", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Este código lee/escribe el Registry de Windows.", true, "Microsoft.Win32.Registry", ".NET moderno es multiplataforma. Migrar a appsettings.json, Azure Key Vault o variables de entorno.", "Windows Registry detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Este código lee/escribe el Registry de Windows.", true, "RegistryKey", ".NET moderno es multiplataforma. Migrar a appsettings.json, Azure Key Vault o variables de entorno.", "Windows Registry detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dependencia de DLL nativa de Windows.", true, "DllImport", "Evaluar si existe alternativa cross-platform. Si no, la app debe correr en Windows.", "COM Interop / P/Invoke detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dependencia de DLL nativa de Windows.", true, "ComVisible", "Evaluar si existe alternativa cross-platform. Si no, la app debe correr en Windows.", "COM Interop / P/Invoke detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Patrones de EF6 que no existen en EF Core.", true, "Database.SetInitializer", "Database.SetInitializer → EF Core Migrations. Revisar lazy loading y transacciones.", "Entity Framework 6 incompatible con EF Core", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, "ManualRequired", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Namespace de EF6 detectado.", true, "System.Data.Entity", "Reemplazar por Microsoft.EntityFrameworkCore. Revisar breaking changes en lazy loading.", "Entity Framework 6 incompatible con EF Core", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clase estática detectada.", true, "static class", "Evaluar si debe ser Scoped, Singleton o Transient en el contenedor de DI de .NET.", "Clase estática — candidata a DI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GDI+ depende de librerías nativas de Windows.", true, "System.Drawing", "En Linux/Azure va a fallar. Migrar a SkiaSharp o ImageSharp.", "System.Drawing / GDI+ detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), ".Result detectado sin async/await.", true, ".Result", "Causa deadlocks en ASP.NET Core. Convertir a async/await con cuidado.", "Código sincrónico bloqueante", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bloquea el thread en lugar de usar async.", true, "Thread.Sleep", "Reemplazar por await Task.Delay() en contexto async.", "Thread.Sleep detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Acceso estático a HttpContext.", true, "HttpContext.Current", "Inyectar IHttpContextAccessor en lugar del static. Puede causar NullReference en .NET moderno.", "HttpContext.Current detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lectura de configuración vía App.config.", true, "ConfigurationManager.AppSettings", "Reemplazar por IConfiguration inyectado. Web.config fue migrado a appsettings.json automáticamente.", "ConfigurationManager detectado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pipeline OWIN detectado.", true, "Microsoft.Owin", "Migrar a middleware nativo de ASP.NET Core. Program.cs fue generado como punto de partida.", "OWIN / Katana middleware", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, "NeedsReview", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pipeline OWIN detectado.", true, "IAppBuilder", "Migrar a middleware nativo de ASP.NET Core. Program.cs fue generado como punto de partida.", "OWIN / Katana middleware", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacklogRules_Pattern",
                table: "BacklogRules",
                column: "Pattern");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacklogRules");
        }
    }
}
