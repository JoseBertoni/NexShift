using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddKnownDeprecatedPackagesTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "KnownDeprecatedPackages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Category = table.Column<string>(maxLength: 50, nullable: false, defaultValue: "Deprecated"),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                SuggestedReplacement = table.Column<string>(maxLength: 300, nullable: true),
                AdvisoryUrl = table.Column<string>(maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_KnownDeprecatedPackages", x => x.Id));

        // Seed — los 44 paquetes hardcodeados actuales
        var now = DateTime.UtcNow;

        var deprecated = new[]
        {
            ("Newtonsoft.Json",                       "System.Text.Json (nativo en .NET)"),
            ("AutoMapper",                            "Mapperly o mapeo manual"),
            ("log4net",                               "Microsoft.Extensions.Logging + Serilog"),
            ("NLog",                                  "Microsoft.Extensions.Logging + Serilog"),
            ("Elmah",                                 "Serilog + middleware de errores ASP.NET Core"),
            ("Elmah.MVC",                             "Serilog + middleware de errores ASP.NET Core"),
            ("elmah.corelibrary",                     "Serilog + middleware de errores ASP.NET Core"),
            ("Unity",                                 "Microsoft.Extensions.DependencyInjection"),
            ("Ninject",                               "Microsoft.Extensions.DependencyInjection"),
            ("Ninject.MVC5",                          "Microsoft.Extensions.DependencyInjection"),
            ("Castle.Windsor",                        "Microsoft.Extensions.DependencyInjection"),
            ("Autofac.Mvc5",                          "Autofac.Extensions.DependencyInjection"),
            ("StructureMap",                          "Microsoft.Extensions.DependencyInjection"),
            ("Microsoft.AspNet.Mvc",                  "Microsoft.AspNetCore.Mvc"),
            ("Microsoft.AspNet.WebApi",               "Microsoft.AspNetCore"),
            ("Microsoft.AspNet.WebApi.Core",          "Microsoft.AspNetCore"),
            ("Microsoft.AspNet.Razor",                "Microsoft.AspNetCore.Razor"),
            ("Microsoft.AspNet.WebPages",             "Microsoft.AspNetCore.Mvc.RazorPages"),
            ("Microsoft.AspNet.Identity.Core",        "Microsoft.AspNetCore.Identity"),
            ("Microsoft.AspNet.Identity.EntityFramework", "Microsoft.AspNetCore.Identity.EntityFrameworkCore"),
            ("Microsoft.AspNet.Identity.Owin",        "Microsoft.AspNetCore.Identity"),
            ("Microsoft.AspNet.Web.Optimization",     "WebOptimizer o bundling nativo"),
            ("Microsoft.Owin",                        "Microsoft.AspNetCore (OWIN reemplazado por middleware nativo)"),
            ("Microsoft.Owin.Host.SystemWeb",         "Microsoft.AspNetCore"),
            ("Microsoft.Owin.Security",               "Microsoft.AspNetCore.Authentication"),
            ("Microsoft.Owin.Security.Cookies",       "Microsoft.AspNetCore.Authentication.Cookies"),
            ("Microsoft.Owin.Security.OAuth",         "Microsoft.AspNetCore.Authentication.OAuth"),
            ("Microsoft.Owin.Security.Facebook",      "Microsoft.AspNetCore.Authentication.Facebook"),
            ("Microsoft.Owin.Security.Google",        "Microsoft.AspNetCore.Authentication.Google"),
            ("Microsoft.Owin.Security.Twitter",       "Microsoft.AspNetCore.Authentication.Twitter"),
            ("Microsoft.Owin.Security.MicrosoftAccount", "Microsoft.AspNetCore.Authentication.MicrosoftAccount"),
            ("Owin",                                  "Microsoft.AspNetCore (middleware nativo)"),
            ("EntityFramework",                       "Microsoft.EntityFrameworkCore"),
            ("MySql.Data",                            "Pomelo.EntityFrameworkCore.MySql"),
            ("RestSharp",                             "System.Net.Http.Json o Refit"),
            ("NUnit",                                 "xUnit o NUnit3"),
            ("MSTest.TestFramework",                  "xUnit o NUnit"),
            ("Microsoft.Web.Infrastructure",          "No requerido en ASP.NET Core"),
            ("WebGrease",                             "No requerido en ASP.NET Core"),
            ("Antlr",                                 "No requerido en ASP.NET Core"),
            ("Respond",                               "No requerido — use CSS moderno"),
            ("Modernizr",                             "No requerido — use feature detection nativo"),
            ("MvcMailer",                             "FluentEmail o MailKit"),
            ("WebActivatorEx",                        "Program.cs con WebApplication.CreateBuilder"),
        };

        foreach (var (name, replacement) in deprecated)
        {
            migrationBuilder.InsertData("KnownDeprecatedPackages",
                columns: ["Id", "Name", "Category", "Reason", "SuggestedReplacement", "IsActive", "CreatedAt", "UpdatedAt"],
                values: [Guid.NewGuid(), name, "Deprecated", "Not compatible with ASP.NET Core / modern .NET", replacement, true, now, now]);
        }

        // Seed — vulnerabilidades de seguridad conocidas
        migrationBuilder.InsertData("KnownDeprecatedPackages",
            columns: ["Id", "Name", "Category", "Reason", "SuggestedReplacement", "AdvisoryUrl", "IsActive", "CreatedAt", "UpdatedAt"],
            values: [Guid.NewGuid(), "MimeKit", "SecurityVulnerability",
                      "Known moderate security vulnerability in versions prior to 4.14.1",
                      "Update to MimeKit >= 4.14.1",
                      "https://github.com/advisories/GHSA-g7hc-96xr-gvvx",
                      true, now, now]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("KnownDeprecatedPackages");
    }
}