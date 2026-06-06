namespace NexShift.Infrastructure.Services.Migrator;

public class GlobalAsaxTransformer
{
    public (string programCs, List<string> changes) Transform(string? globalAsaxContent)
    {
        var changes = new List<string>();
        var hasOwin = globalAsaxContent?.Contains("OwinStartup") ?? false;
        var hasAutofac = globalAsaxContent?.Contains("Autofac") ?? false;
        var hasEF = globalAsaxContent?.Contains("Database.SetInitializer") ?? false;
        var hasRoutes = globalAsaxContent?.Contains("RegisterRoutes") ?? false;
        var hasFilters = globalAsaxContent?.Contains("RegisterGlobalFilters") ?? false;
        var hasBundles = globalAsaxContent?.Contains("BundleConfig") ?? false;

        changes.Add("Global.asax migrado a Program.cs");

        if (hasOwin) changes.Add("OWIN startup detectado — migrado a middleware ASP.NET Core");
        if (hasAutofac) changes.Add("Autofac detectado — migrado a Microsoft.Extensions.DependencyInjection");
        if (hasEF) changes.Add("Database.SetInitializer detectado — usar EF Core Migrations en su lugar");
        if (hasRoutes) changes.Add("Rutas detectadas — migradas a app.MapControllers()");
        if (hasBundles) changes.Add("BundleConfig detectado — bundling no requerido en ASP.NET Core");

        var programCs = GenerateProgramCs(hasOwin, hasAutofac, hasEF, hasRoutes, hasFilters);
        return (programCs, changes);
    }

    private static string GenerateProgramCs(bool hasOwin, bool hasAutofac, bool hasEF, bool hasRoutes, bool hasFilters)
    {
        var usings = new List<string>
        {
            "using Microsoft.AspNetCore.Builder;",
            "using Microsoft.Extensions.DependencyInjection;",
            "using Microsoft.Extensions.Hosting;"
        };

        if (hasAutofac)
            usings.Add("using Autofac.Extensions.DependencyInjection;");

        if (hasEF)
            usings.Add("using Microsoft.EntityFrameworkCore;");

        var services = new List<string>
        {
            "builder.Services.AddControllersWithViews();",
            "builder.Services.AddRazorPages();"
        };

        if (hasAutofac)
        {
            services.Add("");
            services.Add("// TODO: Registrar servicios previamente configurados en Autofac");
            services.Add("// builder.Services.AddScoped<IMyService, MyService>();");
        }

        if (hasEF)
        {
            services.Add("");
            services.Add("// TODO: Configurar EF Core");
            services.Add("// builder.Services.AddDbContext<AppDbContext>(options =>");
            services.Add("//     options.UseSqlServer(builder.Configuration.GetConnectionString(\"Default\")));");
        }

        var middleware = new List<string>
        {
            "app.UseHttpsRedirection();",
            "app.UseStaticFiles();",
            "app.UseRouting();",
            "app.UseAuthentication();",
            "app.UseAuthorization();"
        };

        if (hasRoutes)
            middleware.Add("app.MapControllerRoute(name: \"default\", pattern: \"{controller=Home}/{action=Index}/{id?}\");");

        middleware.Add("app.MapRazorPages();");

        var sb = new System.Text.StringBuilder();

        foreach (var u in usings)
            sb.AppendLine(u);

        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("// ── Services ────────────────────────────────────────────");

        if (hasOwin)
        {
            sb.AppendLine("// OWIN pipeline migrado a middleware nativo de ASP.NET Core");
        }

        foreach (var s in services)
            sb.AppendLine(s);

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// ── Middleware ──────────────────────────────────────────");

        foreach (var m in middleware)
            sb.AppendLine(m);

        sb.AppendLine();
        sb.AppendLine("app.Run();");

        return sb.ToString();
    }
}