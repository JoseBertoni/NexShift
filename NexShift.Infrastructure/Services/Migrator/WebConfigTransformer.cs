using System.Text;
using System.Xml.Linq;

namespace NexShift.Infrastructure.Services.Migrator;

public class WebConfigTransformer
{
    public (string appsettingsJson, List<string> changes) Transform(string webConfigContent)
    {
        var changes = new List<string>();
        var settings = new Dictionary<string, string>();
        var connectionStrings = new Dictionary<string, string>();

        try
        {
            var xml = XDocument.Parse(webConfigContent);

            // Extraer appSettings
            foreach (var add in xml.Descendants("appSettings").Elements("add"))
            {
                var key = add.Attribute("key")?.Value;
                var value = add.Attribute("value")?.Value;
                if (!string.IsNullOrEmpty(key))
                {
                    settings[key] = value ?? string.Empty;
                    changes.Add($"appSetting migrado: {key}");
                }
            }

            // Extraer connectionStrings
            foreach (var add in xml.Descendants("connectionStrings").Elements("add"))
            {
                var name = add.Attribute("name")?.Value;
                var connStr = add.Attribute("connectionString")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    connectionStrings[name] = connStr ?? string.Empty;
                    changes.Add($"ConnectionString migrada: {name}");
                }
            }
        }
        catch
        {
            changes.Add("Web.config no pudo parsearse completamente — revisión manual requerida");
        }

        // Generar appsettings.json
        var sb = new StringBuilder();
        sb.AppendLine("{");

        // ConnectionStrings
        if (connectionStrings.Any())
        {
            sb.AppendLine("  \"ConnectionStrings\": {");
            var csEntries = connectionStrings.Select(kv =>
                $"    \"{EscapeJson(kv.Key)}\": \"{EscapeJson(kv.Value)}\"");
            sb.AppendLine(string.Join(",\n", csEntries));
            sb.AppendLine("  },");
        }

        // AppSettings
        if (settings.Any())
        {
            sb.AppendLine("  \"AppSettings\": {");
            var entries = settings.Select(kv =>
                $"    \"{EscapeJson(kv.Key)}\": \"{EscapeJson(kv.Value)}\"");
            sb.AppendLine(string.Join(",\n", entries));
            sb.AppendLine("  },");
        }

        // Logging por defecto
        sb.AppendLine("  \"Logging\": {");
        sb.AppendLine("    \"LogLevel\": {");
        sb.AppendLine("      \"Default\": \"Information\",");
        sb.AppendLine("      \"Microsoft.AspNetCore\": \"Warning\"");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"AllowedHosts\": \"*\"");
        sb.AppendLine("}");

        changes.Add("Web.config migrado a appsettings.json");

        return (sb.ToString(), changes);
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}