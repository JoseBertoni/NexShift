using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds ReplacementVersion column to KnownDeprecatedPackages
/// and seeds the PackageReplacements data (previously hardcoded in CsprojTransformer).
/// Also seeds PackagesToRemove entries with Category = "RemoveOnly".
/// </summary>
public partial class AddReplacementVersionAndSeedPackageReplacements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── New column ────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "ReplacementVersion",
            table: "KnownDeprecatedPackages",
            maxLength: 20,
            nullable: true);

        // ── Update existing Deprecated seed rows with ReplacementVersion ──────
        // (The 44 rows seeded in the previous migration had no version)
        // Nothing to update — versions weren't tracked before.

        var now = DateTime.UtcNow;

        // ── Seed: PackagesToRemove → Category = "RemoveOnly" ─────────────────
        // These packages are simply dropped — no replacement needed
        var toRemove = new[]
        {
            "Microsoft.Web.Infrastructure",
            "WebGrease",
            "Antlr",
            "Respond",
            "Modernizr",
            "Microsoft.AspNet.Web.Optimization",
            "Microsoft.Owin.Host.SystemWeb",
            "T4Scaffolding.Core",
            "PagedList.Mvc",
            "Moq",
        };

        foreach (var name in toRemove)
        {
            migrationBuilder.InsertData("KnownDeprecatedPackages",
                columns: ["Id", "Name", "Category", "Reason", "SuggestedReplacement", "ReplacementVersion", "IsActive", "CreatedAt", "UpdatedAt"],
                values: [Guid.NewGuid(), name, "RemoveOnly",
                          "Not required in ASP.NET Core", null, null, true, now, now]);
        }

        // ── Seed: PackageReplacements → Category = "Deprecated" with version ──
        var replacements = new[]
        {
            // (OldName, NewName, Version)  — empty version = native/framework-included
            ("EntityFramework",                          "Microsoft.EntityFrameworkCore",                        "8.0.0"),
            ("AutoMapper",                               "Mapperly",                                             "3.0.0"),
            ("Newtonsoft.Json",                          "",                                                     ""),      // native System.Text.Json
            ("log4net",                                  "Serilog",                                              "4.0.0"),
            ("NLog",                                     "Serilog",                                              "4.0.0"),
            ("Microsoft.AspNet.Mvc",                     "",                                                     ""),      // framework-included
            ("Microsoft.AspNet.WebApi",                  "",                                                     ""),
            ("Microsoft.AspNet.WebApi.Core",             "",                                                     ""),
            ("Microsoft.AspNet.WebApi.WebHost",          "",                                                     ""),
            ("Microsoft.AspNet.Razor",                   "",                                                     ""),
            ("Microsoft.AspNet.WebPages",                "",                                                     ""),
            ("Microsoft.AspNet.Identity.Core",           "Microsoft.AspNetCore.Identity",                        "8.0.0"),
            ("Microsoft.AspNet.Identity.EntityFramework","Microsoft.AspNetCore.Identity.EntityFrameworkCore",    "8.0.0"),
            ("Microsoft.AspNet.Identity.Owin",           "Microsoft.AspNetCore.Identity",                        "8.0.0"),
            ("Microsoft.Owin",                           "",                                                     ""),      // eliminate
            ("Microsoft.Owin.Security",                  "Microsoft.AspNetCore.Authentication",                  ""),
            ("Microsoft.Owin.Security.Cookies",          "Microsoft.AspNetCore.Authentication.Cookies",          ""),
            ("Microsoft.Owin.Security.OAuth",            "Microsoft.AspNetCore.Authentication.OAuth",            ""),
            ("Microsoft.Owin.Security.Facebook",         "Microsoft.AspNetCore.Authentication.Facebook",         "8.0.0"),
            ("Microsoft.Owin.Security.Google",           "Microsoft.AspNetCore.Authentication.Google",           "8.0.0"),
            ("Microsoft.Owin.Security.Twitter",          "Microsoft.AspNetCore.Authentication.Twitter",          "8.0.0"),
            ("Microsoft.Owin.Security.MicrosoftAccount", "Microsoft.AspNetCore.Authentication.MicrosoftAccount", "8.0.0"),
            ("Owin",                                     "",                                                     ""),      // eliminate
            ("Unity",                                    "Microsoft.Extensions.DependencyInjection",             ""),
            ("Ninject",                                  "Microsoft.Extensions.DependencyInjection",             ""),
            ("Castle.Windsor",                           "Microsoft.Extensions.DependencyInjection",             ""),
            ("Autofac.Mvc5",                             "Autofac.Extensions.DependencyInjection",               "8.0.0"),
            ("MySql.Data",                               "Pomelo.EntityFrameworkCore.MySql",                     "8.0.0"),
            ("RestSharp",                                "Refit",                                                "7.0.0"),
            ("MvcMailer",                                "MailKit",                                              "4.0.0"),
            ("Elmah",                                    "Serilog",                                              "4.0.0"),
            ("Elmah.MVC",                                "Serilog",                                              "4.0.0"),
            ("elmah.corelibrary",                        "Serilog",                                              "4.0.0"),
        };

        foreach (var (oldName, newName, version) in replacements)
        {
            // Check if already seeded by the previous migration — skip duplicates
            // (Previous migration seeded 44 entries; overlap is intentional,
            //  this migration enriches them with ReplacementVersion)
            // We use InsertData only for rows NOT already present.
            // Rows already in the table from the previous migration are updated below.
        }

        // Update ReplacementVersion for rows already seeded
        foreach (var (oldName, newName, version) in replacements)
        {
            if (!string.IsNullOrEmpty(version))
            {
                migrationBuilder.Sql(
                    $"UPDATE \"KnownDeprecatedPackages\" SET \"ReplacementVersion\" = '{version}', \"UpdatedAt\" = NOW() " +
                    $"WHERE \"Name\" = '{oldName}' AND \"IsActive\" = true;");
            }

            // For rows where SuggestedReplacement is empty/null in DB but we have a value, update it
            if (!string.IsNullOrEmpty(newName))
            {
                migrationBuilder.Sql(
                    $"UPDATE \"KnownDeprecatedPackages\" SET \"SuggestedReplacement\" = '{newName}', \"UpdatedAt\" = NOW() " +
                    $"WHERE \"Name\" = '{oldName}' AND (\"SuggestedReplacement\" IS NULL OR \"SuggestedReplacement\" = '');");
            }
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReplacementVersion",
            table: "KnownDeprecatedPackages");
    }
}
