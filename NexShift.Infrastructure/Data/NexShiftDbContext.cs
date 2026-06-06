using Microsoft.EntityFrameworkCore;
using NexShift.Core.Entities;

namespace NexShift.Infrastructure.Data;

public class NexShiftDbContext : DbContext
{
    public NexShiftDbContext(DbContextOptions<NexShiftDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<MigrationJob> MigrationJobs => Set<MigrationJob>();
    public DbSet<MigrationIssue> MigrationIssues => Set<MigrationIssue>();
    public DbSet<NugetPackage> NugetPackages => Set<NugetPackage>();
    public DbSet<BacklogRule> BacklogRules => Set<BacklogRule>();
    public DbSet<TransformationRule> TransformationRules => Set<TransformationRule>();
    public DbSet<KnownDeprecatedPackage> KnownDeprecatedPackages => Set<KnownDeprecatedPackage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitHubId).IsRequired();
            entity.Property(e => e.Login).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.GitHubAccessToken).IsRequired();
            entity.HasIndex(e => e.GitHubId).IsUnique();
            entity.HasIndex(e => e.Login).IsUnique();
        });

        // Repository
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Repositories)
                  .HasForeignKey(e => e.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });

        modelBuilder.Entity<NugetPackage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SuggestedReplacement).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
        });

        // MigrationJob
        modelBuilder.Entity<MigrationJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetFramework).IsRequired().HasMaxLength(20);
            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.MigrationJobs)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // MigrationIssue
        modelBuilder.Entity<MigrationIssue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).IsRequired();
            entity.HasOne(e => e.MigrationJob)
                  .WithMany(j => j.Issues)
                  .HasForeignKey(e => e.MigrationJobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // BacklogRule
        modelBuilder.Entity<BacklogRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Reason).IsRequired();
            entity.HasIndex(e => e.Pattern);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Seed data — todas las reglas del BacklogDetector
            entity.HasData(
                new BacklogRule { Id = 1, Pattern = "ServiceContract", Category = "ManualRequired", Title = "WCF Service detectado", Description = "Este archivo usa WCF que no existe en .NET moderno.", Reason = "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 2, Pattern = "OperationContract", Category = "ManualRequired", Title = "WCF Service detectado", Description = "Este archivo usa WCF que no existe en .NET moderno.", Reason = "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 3, Pattern = "System.ServiceModel", Category = "ManualRequired", Title = "WCF Service detectado", Description = "Este archivo usa WCF que no existe en .NET moderno.", Reason = "Migrar manualmente a gRPC (recomendado) o REST. Ver: aka.ms/wcf-migration", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 4, Pattern = "System.Web.UI", Category = "ManualRequired", Title = "ASP.NET Web Forms detectado", Description = "Los controles Web Forms no tienen equivalente en .NET moderno.", Reason = "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 5, Pattern = "Page_Load", Category = "ManualRequired", Title = "ASP.NET Web Forms detectado", Description = "Los controles Web Forms no tienen equivalente en .NET moderno.", Reason = "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 6, Pattern = "ViewState", Category = "ManualRequired", Title = "ASP.NET Web Forms detectado", Description = "Los controles Web Forms no tienen equivalente en .NET moderno.", Reason = "La lógica del Code Behind puede rescatarse. La UI debe reescribirse en Razor Pages o Blazor.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 7, Pattern = "FormsAuthentication", Category = "ManualRequired", Title = "Autenticación legacy detectada", Description = "FormsAuthentication no existe en .NET moderno.", Reason = "Decisión requerida: ¿JWT + Identity, Azure AD, Auth0? El motor no puede elegir por vos.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 8, Pattern = "WindowsIdentity", Category = "ManualRequired", Title = "Autenticación legacy detectada", Description = "WindowsIdentity requiere decisión arquitectural.", Reason = "Decisión requerida: ¿JWT + Identity, Azure AD, Auth0? El motor no puede elegir por vos.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 9, Pattern = "Microsoft.Win32.Registry", Category = "ManualRequired", Title = "Windows Registry detectado", Description = "Este código lee/escribe el Registry de Windows.", Reason = ".NET moderno es multiplataforma. Migrar a appsettings.json, Azure Key Vault o variables de entorno.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 10, Pattern = "RegistryKey", Category = "ManualRequired", Title = "Windows Registry detectado", Description = "Este código lee/escribe el Registry de Windows.", Reason = ".NET moderno es multiplataforma. Migrar a appsettings.json, Azure Key Vault o variables de entorno.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 11, Pattern = "DllImport", Category = "ManualRequired", Title = "COM Interop / P/Invoke detectado", Description = "Dependencia de DLL nativa de Windows.", Reason = "Evaluar si existe alternativa cross-platform. Si no, la app debe correr en Windows.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 12, Pattern = "ComVisible", Category = "ManualRequired", Title = "COM Interop / P/Invoke detectado", Description = "Dependencia de DLL nativa de Windows.", Reason = "Evaluar si existe alternativa cross-platform. Si no, la app debe correr en Windows.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 13, Pattern = "Database.SetInitializer", Category = "ManualRequired", Title = "Entity Framework 6 incompatible con EF Core", Description = "Patrones de EF6 que no existen en EF Core.", Reason = "Database.SetInitializer → EF Core Migrations. Revisar lazy loading y transacciones.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 14, Pattern = "System.Data.Entity", Category = "ManualRequired", Title = "Entity Framework 6 incompatible con EF Core", Description = "Namespace de EF6 detectado.", Reason = "Reemplazar por Microsoft.EntityFrameworkCore. Revisar breaking changes en lazy loading.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 15, Pattern = "static class", Category = "NeedsReview", Title = "Clase estática — candidata a DI", Description = "Clase estática detectada.", Reason = "Evaluar si debe ser Scoped, Singleton o Transient en el contenedor de DI de .NET.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 16, Pattern = "System.Drawing", Category = "NeedsReview", Title = "System.Drawing / GDI+ detectado", Description = "GDI+ depende de librerías nativas de Windows.", Reason = "En Linux/Azure va a fallar. Migrar a SkiaSharp o ImageSharp.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 17, Pattern = ".Result", Category = "NeedsReview", Title = "Código sincrónico bloqueante", Description = ".Result detectado sin async/await.", Reason = "Causa deadlocks en ASP.NET Core. Convertir a async/await con cuidado.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 18, Pattern = "Thread.Sleep", Category = "NeedsReview", Title = "Thread.Sleep detectado", Description = "Bloquea el thread en lugar de usar async.", Reason = "Reemplazar por await Task.Delay() en contexto async.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 19, Pattern = "HttpContext.Current", Category = "NeedsReview", Title = "HttpContext.Current detectado", Description = "Acceso estático a HttpContext.", Reason = "Inyectar IHttpContextAccessor en lugar del static. Puede causar NullReference en .NET moderno.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 20, Pattern = "ConfigurationManager.AppSettings", Category = "NeedsReview", Title = "ConfigurationManager detectado", Description = "Lectura de configuración vía App.config.", Reason = "Reemplazar por IConfiguration inyectado. Web.config fue migrado a appsettings.json automáticamente.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 21, Pattern = "Microsoft.Owin", Category = "NeedsReview", Title = "OWIN / Katana middleware", Description = "Pipeline OWIN detectado.", Reason = "Migrar a middleware nativo de ASP.NET Core. Program.cs fue generado como punto de partida.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 22, Pattern = "IAppBuilder", Category = "NeedsReview", Title = "OWIN / Katana middleware", Description = "Pipeline OWIN detectado.", Reason = "Migrar a middleware nativo de ASP.NET Core. Program.cs fue generado como punto de partida.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // ── Nuevos patrones: APIs eliminadas o peligrosas ────────────────────────
                new BacklogRule { Id = 23, Pattern = "BinaryFormatter", Category = "ManualRequired", Title = "BinaryFormatter (vulnerabilidad de seguridad)", Description = "BinaryFormatter está obsoleto y deshabilitado por defecto en .NET moderno por riesgo de ejecución remota de código.", Reason = "Migrar a System.Text.Json, XmlSerializer o Google Protocol Buffers. Ver: aka.ms/binaryformatter", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 24, Pattern = "Thread.Abort", Category = "ManualRequired", Title = "Thread.Abort eliminado en .NET moderno", Description = "Thread.Abort lanza ThreadAbortException, que fue eliminada en .NET 5+.", Reason = "Reemplazar con CancellationToken para cancelación cooperativa. Es un cambio de patrón de diseño.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 25, Pattern = "AppDomain.CreateDomain", Category = "ManualRequired", Title = "AppDomain.CreateDomain no disponible en .NET moderno", Description = "La creación de AppDomains secundarios fue eliminada. Solo existe el AppDomain raíz.", Reason = "Usar AssemblyLoadContext como alternativa para aislamiento de ensamblados.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 26, Pattern = "System.Runtime.Remoting", Category = "ManualRequired", Title = ".NET Remoting completamente eliminado", Description = "System.Runtime.Remoting no existe en .NET moderno.", Reason = "Migrar a gRPC (recomendado) o WCF/CoreWCF para comunicación entre procesos.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 27, Pattern = "System.Web.Security", Category = "ManualRequired", Title = "System.Web.Security eliminado", Description = "El namespace System.Web.Security no existe en .NET moderno.", Reason = "Migrar a Microsoft.AspNetCore.Identity o ASP.NET Core Authentication. Requiere decisión arquitectural.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 28, Pattern = "System.Diagnostics.EventLog", Category = "NeedsReview", Title = "EventLog (solo Windows)", Description = "System.Diagnostics.EventLog solo funciona en Windows.", Reason = "Reemplazar con Microsoft.Extensions.Logging + Serilog/NLog para logging cross-platform.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 29, Pattern = "Server.MapPath", Category = "NeedsReview", Title = "Server.MapPath (IIS-specific)", Description = "Server.MapPath depende de IIS y no está disponible en ASP.NET Core.", Reason = "Reemplazar con IWebHostEnvironment.WebRootPath o IHostEnvironment.ContentRootPath.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new BacklogRule { Id = 30, Pattern = "HttpWebRequest", Category = "NeedsReview", Title = "HttpWebRequest obsoleto", Description = "HttpWebRequest es una API legacy de networking.", Reason = "Reemplazar con HttpClient inyectado via IHttpClientFactory. Es más eficiente y testeable.", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        modelBuilder.Entity<TransformationRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Replacement).HasMaxLength(500);
            entity.Property(e => e.Description).IsRequired();
            entity.HasIndex(e => e.Pattern);
            entity.HasIndex(e => e.Priority);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasData(
                // ── NeedsAI = false → pure regex/replace ──────────────────────────

                // System.Web usings
                new TransformationRule { Id = 1, Pattern = "using System.Web.Mvc;", Replacement = "using Microsoft.AspNetCore.Mvc;", NeedsAI = false, IsRegex = false, Priority = 10, Description = "System.Web.Mvc → Microsoft.AspNetCore.Mvc", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 2, Pattern = "using System.Web.Http;", Replacement = "using Microsoft.AspNetCore.Mvc;", NeedsAI = false, IsRegex = false, Priority = 10, Description = "System.Web.Http → Microsoft.AspNetCore.Mvc", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 3, Pattern = "using System.Web.Optimization;", Replacement = "// REMOVED: System.Web.Optimization - not available in .NET modern", NeedsAI = false, IsRegex = false, Priority = 10, Description = "System.Web.Optimization → removed", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 4, Pattern = "using System.Web.Routing;", Replacement = "// REMOVED: System.Web.Routing - use ASP.NET Core routing", NeedsAI = false, IsRegex = false, Priority = 10, Description = "System.Web.Routing → removed", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // Controller base classes
                new TransformationRule { Id = 5, Pattern = ": ApiController", Replacement = ": ControllerBase", NeedsAI = false, IsRegex = false, Priority = 20, Description = "ApiController → ControllerBase", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 6, Pattern = ": Controller", Replacement = ": Controller", NeedsAI = false, IsRegex = false, Priority = 20, Description = "MVC Controller kept as-is", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // HTTP Attributes
                new TransformationRule { Id = 7, Pattern = "[System.Web.Http.HttpGet]", Replacement = "[HttpGet]", NeedsAI = false, IsRegex = false, Priority = 20, Description = "System.Web.Http.HttpGet → HttpGet", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 8, Pattern = "[System.Web.Http.HttpPost]", Replacement = "[HttpPost]", NeedsAI = false, IsRegex = false, Priority = 20, Description = "System.Web.Http.HttpPost → HttpPost", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 9, Pattern = "[System.Web.Http.HttpPut]", Replacement = "[HttpPut]", NeedsAI = false, IsRegex = false, Priority = 20, Description = "System.Web.Http.HttpPut → HttpPut", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 10, Pattern = "[System.Web.Http.HttpDelete]", Replacement = "[HttpDelete]", NeedsAI = false, IsRegex = false, Priority = 20, Description = "System.Web.Http.HttpDelete → HttpDelete", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // RoutePrefix → Route
                new TransformationRule { Id = 11, Pattern = "RoutePrefix(", Replacement = "Route(", NeedsAI = false, IsRegex = false, Priority = 20, Description = "RoutePrefix → Route", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // WebConfigurationManager → IConfiguration (simple cases)
                new TransformationRule { Id = 12, Pattern = "WebConfigurationManager.AppSettings", Replacement = "// TODO: NEXSHIFT - Inject IConfiguration and use _configuration[\"key\"]", NeedsAI = false, IsRegex = false, Priority = 30, Description = "WebConfigurationManager → IConfiguration comment", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 13, Pattern = "ConfigurationManager.AppSettings", Replacement = "// TODO: NEXSHIFT - Inject IConfiguration and use _configuration[\"key\"]", NeedsAI = false, IsRegex = false, Priority = 30, Description = "ConfigurationManager → IConfiguration comment", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // HttpResponseMessage → IActionResult
                new TransformationRule { Id = 14, Pattern = "HttpResponseMessage", Replacement = "IActionResult", NeedsAI = false, IsRegex = false, Priority = 30, Description = "HttpResponseMessage → IActionResult", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // IHttpActionResult → IActionResult
                new TransformationRule { Id = 15, Pattern = "IHttpActionResult", Replacement = "IActionResult", NeedsAI = false, IsRegex = false, Priority = 30, Description = "IHttpActionResult → IActionResult", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // Request.CreateResponse → Ok/BadRequest
                new TransformationRule { Id = 16, Pattern = "Request.CreateResponse(HttpStatusCode.OK,", Replacement = "Ok(", NeedsAI = false, IsRegex = false, Priority = 30, Description = "Request.CreateResponse OK → Ok()", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 17, Pattern = "Request.CreateResponse(HttpStatusCode.BadRequest,", Replacement = "BadRequest(", NeedsAI = false, IsRegex = false, Priority = 30, Description = "Request.CreateResponse BadRequest → BadRequest()", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new TransformationRule { Id = 18, Pattern = "Request.CreateResponse(HttpStatusCode.NotFound)", Replacement = "NotFound()", NeedsAI = false, IsRegex = false, Priority = 30, Description = "Request.CreateResponse NotFound → NotFound()", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // ── NeedsAI = true → complex patterns Claude must handle ───────────

                // HttpContext.Current (complex — needs DI refactor)
                new TransformationRule { Id = 19, Pattern = "HttpContext.Current", Replacement = null, NeedsAI = true, IsRegex = false, Priority = 50, Description = "HttpContext.Current → needs IHttpContextAccessor DI", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // GlobalConfiguration (complex — needs full refactor)
                new TransformationRule { Id = 20, Pattern = "GlobalConfiguration.Configure", Replacement = null, NeedsAI = true, IsRegex = false, Priority = 50, Description = "GlobalConfiguration → needs Program.cs refactor", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // System.Drawing (complex — needs SkiaSharp/ImageSharp migration)
                new TransformationRule { Id = 21, Pattern = "System.Drawing", Replacement = null, NeedsAI = true, IsRegex = false, Priority = 50, Description = "System.Drawing → needs SkiaSharp or ImageSharp migration", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },

                // Microsoft.Owin (complex — needs full middleware rewrite)
                new TransformationRule { Id = 22, Pattern = "Microsoft.Owin", Replacement = null, NeedsAI = true, IsRegex = false, Priority = 50, Description = "Microsoft.Owin → needs ASP.NET Core middleware rewrite", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
        });
    }
}