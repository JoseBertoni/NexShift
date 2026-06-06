using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexShift.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexShift.Infrastructure.Services
{
    public class PdfReportService : IPdfReportService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PdfReportService> _logger;

        public PdfReportService(IConfiguration configuration, ILogger<PdfReportService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<byte[]> GenerateDebtReport(ProjectAnalysisResult analysis, string repoUrl)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var deprecatedPackages = analysis.Packages.Where(p => p.IsDeprecated).ToList();

            var (estimatedManualDays, estimatedNexShiftMinutes, costPerDay, justification) = await EstimateWithClaude(analysis);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(content => ComposeContent(content, analysis, repoUrl, deprecatedPackages, estimatedManualDays, estimatedNexShiftMinutes, costPerDay, justification));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("NexShift").FontSize(24).Bold().FontColor("#6366F1");
                        c.Item().Text("Technical Debt Report").FontSize(14).FontColor("#64748B");
                    });

                    row.ConstantItem(120).Column(c =>
                    {
                        c.Item().AlignRight().Text(DateTime.UtcNow.ToString("MMM dd, yyyy")).FontSize(10).FontColor("#94A3B8");
                        c.Item().AlignRight().Text("Confidential").FontSize(9).FontColor("#EF4444");
                    });
                });

                col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#6366F1");
            });
        }

        private void ComposeContent(IContainer container, ProjectAnalysisResult analysis, string repoUrl,
            List<PackageInfo> deprecatedPackages, int estimatedManualDays, int estimatedNexShiftMinutes, int costPerDay, string justification)
        {
            container.PaddingTop(20).Column(col =>
            {
                // Repo info
                col.Item().Background("#F8FAFC").Padding(12).Column(info =>
                {
                    info.Item().Text("Repository Analysis").FontSize(14).Bold().FontColor("#1E293B");
                    info.Item().PaddingTop(4).Text(repoUrl).FontSize(9).FontColor("#6366F1");
                });

                col.Item().PaddingTop(20).Text("Executive Summary").FontSize(14).Bold().FontColor("#1E293B");
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");

                // Score cards
                col.Item().PaddingTop(12).Row(row =>
                {
                    ScoreCard(row.RelativeItem(), "Migration Score", $"{analysis.MigrationScore}/100",
                        analysis.MigrationScore < 40 ? "#EF4444" : analysis.MigrationScore < 70 ? "#F59E0B" : "#10B981");

                    row.ConstantItem(12);

                    ScoreCard(row.RelativeItem(), "Framework", analysis.DetectedFramework, "#6366F1");

                    row.ConstantItem(12);

                    ScoreCard(row.RelativeItem(), "Deprecated Packages",
                        $"{deprecatedPackages.Count}/{analysis.Packages.Count}", "#F59E0B");

                    row.ConstantItem(12);

                    ScoreCard(row.RelativeItem(), "C# Files", analysis.CsFiles.Count.ToString(), "#64748B");
                });

                // ROI Section
                col.Item().PaddingTop(24).Text("Migration Time Estimate").FontSize(14).Bold().FontColor("#1E293B");
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");

                col.Item().PaddingTop(12).Row(row =>
                {
                    // Manual
                    row.RelativeItem().Border(1).BorderColor("#FCA5A5").Background("#FEF2F2").Padding(16).Column(c =>
                    {
                        c.Item().Text("Manual Migration").Bold().FontColor("#DC2626");
                        c.Item().PaddingTop(8).Text($"~{estimatedManualDays} days").FontSize(22).Bold().FontColor("#DC2626");
                        c.Item().PaddingTop(4).Text($"~{estimatedManualDays / 5} weeks of developer time").FontSize(9).FontColor("#94A3B8");
                        c.Item().PaddingTop(4).Text($"Est. cost: ${estimatedManualDays * costPerDay:N0} USD").FontSize(9).FontColor("#94A3B8");
                    });

                    row.ConstantItem(16);

                    // NexShift
                    row.RelativeItem().Border(1).BorderColor("#6EE7B7").Background("#F0FDF4").Padding(16).Column(c =>
                    {
                        c.Item().Text("With NexShift").Bold().FontColor("#059669");
                        c.Item().PaddingTop(8).Text($"~{estimatedNexShiftMinutes} min").FontSize(22).Bold().FontColor("#059669");
                        c.Item().PaddingTop(4).Text("Automated transformation").FontSize(9).FontColor("#94A3B8");
                        c.Item().PaddingTop(4).Text($"Savings: ${estimatedManualDays * costPerDay:N0} USD").FontSize(9).FontColor("#059669").Bold();
                    });
                });

                // Justificación de Claude
                col.Item().PaddingTop(12).Background("#F0F9FF").Padding(12)
                    .Text($"💡 {justification}").FontSize(9).FontColor("#0369A1").Italic();

                // Deprecated packages table
                if (deprecatedPackages.Any())
                {
                    col.Item().PaddingTop(24).Text("Deprecated Packages").FontSize(14).Bold().FontColor("#1E293B");
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(4);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#6366F1").Padding(8).Text("Package").Bold().FontColor(Colors.White);
                            header.Cell().Background("#6366F1").Padding(8).Text("Version").Bold().FontColor(Colors.White);
                            header.Cell().Background("#6366F1").Padding(8).Text("Suggested Replacement").Bold().FontColor(Colors.White);
                        });

                        var isEven = false;
                        foreach (var pkg in deprecatedPackages)
                        {
                            var bg = isEven ? "#F8FAFC" : "#FFFFFF";
                            table.Cell().Background(bg).Padding(8).Text(pkg.Name).FontSize(9);
                            table.Cell().Background(bg).Padding(8).Text(pkg.Version).FontSize(9).FontColor("#94A3B8");
                            table.Cell().Background(bg).Padding(8).Text(pkg.SuggestedReplacement ?? "Manual review required").FontSize(9).FontColor("#059669");
                            isEven = !isEven;
                        }
                    });
                }

                // Next steps
                col.Item().PaddingTop(24).Text("Recommended Next Steps").FontSize(14).Bold().FontColor("#1E293B");
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");

                col.Item().PaddingTop(12).Column(stepsCol =>
                {
                    foreach (var (num, text) in GetSteps())
                    {
                        stepsCol.Item().PaddingBottom(6).Row(row =>
                        {
                            row.ConstantItem(24).Background("#6366F1").AlignCenter().AlignMiddle()
                                .Text(num).Bold().FontColor(Colors.White).FontSize(9);
                            row.ConstantItem(8);
                            row.RelativeItem().AlignMiddle().Text(text).FontSize(9).FontColor("#374151");
                        });
                    }
                });
            });
        }

        private (string num, string text)[] GetSteps() => new[]
        {
            ("1", "Run NexShift migration to automatically transform .csproj files and replace deprecated packages"),
            ("2", "Review generated Program.cs and configure dependency injection manually"),
            ("3", "Update EF Core migrations and test database connectivity"),
            ("4", "Run dotnet build and fix remaining compilation errors"),
            ("5", "Execute test suite and verify business logic integrity"),
        };

        private void ComposeFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor("#E2E8F0");
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text("Generated by NexShift — nexshift.dev").FontSize(8).FontColor("#94A3B8");
                    row.ConstantItem(100).AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor("#94A3B8");
                        text.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                        text.Span(" of ").FontSize(8).FontColor("#94A3B8");
                        text.TotalPages().FontSize(8).FontColor("#94A3B8");
                    });
                });
            });
        }

        private void ScoreCard(IContainer container, string label, string value, string color)
        {
            container.Border(1).BorderColor("#E2E8F0").Padding(12).Column(c =>
            {
                c.Item().Text(label).FontSize(9).FontColor("#64748B");
                c.Item().PaddingTop(4).Text(value).FontSize(18).Bold().FontColor(color);
            });
        }

        private async Task<(int manualDays, int nexshiftMinutes, int costPerDay, string justification)> EstimateWithClaude(ProjectAnalysisResult analysis)
        {
            try
            {
                var apiKey = _configuration["Anthropic:ApiKey"];
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                var prompt = $"""
                    You are a .NET migration expert. Analyze this legacy project and estimate migration effort.
    
                    Project data:
                    - Framework: {analysis.DetectedFramework}
                    - Migration Score: {analysis.MigrationScore}/100
                    - Total C# files: {analysis.CsFiles.Count}
                    - Total projects: {analysis.CsprojFiles.Count}
                    - Deprecated packages: {analysis.Packages.Count(p => p.IsDeprecated)}/{analysis.Packages.Count}
                    - Is Legacy: {analysis.IsLegacy}
                    - Deprecated packages list: {string.Join(", ", analysis.Packages.Where(p => p.IsDeprecated).Select(p => p.Name))}
    
                    Respond ONLY with a JSON object with these exact fields:
                    manualDays (integer), nexshiftMinutes (integer), costPerDay (integer), justification (string).
                    No markdown, no explanation, just the JSON object.
                    """;

                var body = new
                {
                    model = "claude-sonnet-4-5",
                    max_tokens = 500,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", body);
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var text = data.GetProperty("content")[0].GetProperty("text").GetString()!;
                text = text.Trim();
                if (text.StartsWith("```json")) text = text.Substring(7);
                if (text.StartsWith("```")) text = text.Substring(3);
                if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3);
                text = text.Trim();
                var result = JsonSerializer.Deserialize<JsonElement>(text);

                return (
                    result.GetProperty("manualDays").GetInt32(),
                    result.GetProperty("nexshiftMinutes").GetInt32(),
                    result.GetProperty("costPerDay").GetInt32(),
                    result.GetProperty("justification").GetString()!
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error llamando a Claude. Response text: {Text}", "ver abajo");
                return (CalculateManualDaysFallback(analysis), CalculateNexShiftMinutesFallback(analysis), 800, "Estimated based on project complexity metrics.");
            }

        }

        private int CalculateManualDaysFallback(ProjectAnalysisResult analysis)
        {
            var days = 0;
            days += analysis.Packages.Count(p => p.IsDeprecated) * 2;
            days += analysis.CsFiles.Count / 10;
            days += analysis.CsprojFiles.Count * 3;
            if (analysis.IsLegacy) days += 10;
            return Math.Max(5, days);
        }

        private int CalculateNexShiftMinutesFallback(ProjectAnalysisResult analysis)
        {
            var minutes = 2;
            minutes += analysis.CsFiles.Count / 50;
            return Math.Max(2, minutes);
        }
    }
}