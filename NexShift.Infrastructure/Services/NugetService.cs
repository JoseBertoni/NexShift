using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;
using System.Text.Json;

namespace NexShift.Infrastructure.Services;

public class NugetService : INugetService
{
    private readonly NexShiftDbContext _db;
    private readonly ILogger<NugetService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IKnownDeprecatedPackageRepository _knownRepo;

    public NugetService(
        NexShiftDbContext db,
        ILogger<NugetService> logger,
        IHttpClientFactory httpClientFactory,
        IKnownDeprecatedPackageRepository knownRepo)
    {
        _db = db;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _knownRepo = knownRepo;
    }

    public async Task<NugetPackage?> GetPackageInfoAsync(string packageName)
    {
        return await _db.NugetPackages
            .FirstOrDefaultAsync(p => p.Name.ToLower() == packageName.ToLower());
    }

    public async Task<bool> CheckAndUpdatePackageAsync(string packageName)
    {
        try
        {
            // ── 1. Consultar API de NuGet ─────────────────────────────────
            var url = $"https://api.nuget.org/v3/registration5-semver1/{packageName.ToLower()}/index.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Package {Package} not found on NuGet", packageName);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // ── 2. Obtener última versión y compatibilidad ─────────────────
            string? latestVersion = null;
            bool supportsModernNet = false;

            if (data.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var lastPage = items[items.GetArrayLength() - 1];
                if (lastPage.TryGetProperty("items", out var pageItems) && pageItems.GetArrayLength() > 0)
                {
                    var lastItem = pageItems[pageItems.GetArrayLength() - 1];
                    if (lastItem.TryGetProperty("catalogEntry", out var entry))
                    {
                        latestVersion = entry.TryGetProperty("version", out var v) ? v.GetString() : null;

                        if (entry.TryGetProperty("dependencyGroups", out var deps))
                        {
                            foreach (var dep in deps.EnumerateArray())
                            {
                                var tf = dep.TryGetProperty("targetFramework", out var t) ? t.GetString() : "";
                                if (tf != null && (tf.Contains("net8") || tf.Contains("net9") || tf.Contains("netstandard2")))
                                {
                                    supportsModernNet = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // ── 3. Actualizar o crear en BD ───────────────────────────────
            var existing = await _db.NugetPackages
                .FirstOrDefaultAsync(p => p.Name.ToLower() == packageName.ToLower());

            if (existing != null)
            {
                existing.LatestVersion = latestVersion;
                existing.LastChecked = DateTime.UtcNow;

                if (!existing.IsManuallyAdded)
                    existing.IsDeprecated = !supportsModernNet;
            }
            else
            {
                existing = new NugetPackage
                {
                    Id = Guid.NewGuid(),
                    Name = packageName,
                    LatestVersion = latestVersion,
                    IsDeprecated = !supportsModernNet,
                    LastChecked = DateTime.UtcNow,
                    IsManuallyAdded = false
                };
                _db.NugetPackages.Add(existing);
            }

            // ── 4. Cruzar con KnownDeprecatedPackages ─────────────────────
            // Esto sobreescribe IsDeprecated si el paquete está en nuestra lista
            // (tiene prioridad sobre la detección automática de NuGet)
            var knownEntry = await _knownRepo.GetByNameAsync(packageName);
            if (knownEntry != null)
            {
                existing.IsDeprecated = true;
                existing.Reason = knownEntry.Reason;
                existing.SuggestedReplacement = knownEntry.SuggestedReplacement;

                if (knownEntry.Category == "SecurityVulnerability")
                    _logger.LogWarning(
                        "Security vulnerability detected: {Package} — Advisory: {Url}",
                        packageName, knownEntry.AdvisoryUrl);
                else
                    _logger.LogInformation(
                        "Package {Package} flagged as deprecated — {Reason}",
                        packageName, knownEntry.Reason);
            }

            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking NuGet package {Package}", packageName);
            return false;
        }
    }
}