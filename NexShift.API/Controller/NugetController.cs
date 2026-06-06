using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;
using NexShift.Infrastructure.Services;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NugetController : ControllerBase
{
    private readonly INugetService _nugetService;
    private readonly NexShiftDbContext _db;

    public NugetController(INugetService nugetService, NexShiftDbContext db)
    {
        _nugetService = nugetService;
        _db = db;
    }

    
    // Verificar un paquete específico contra NuGet.org
    [HttpPost("check/{packageName}")]
    public async Task<IActionResult> CheckPackage(string packageName)
    {
        var success = await _nugetService.CheckAndUpdatePackageAsync(packageName);
        if (!success) return NotFound(new { message = $"Package {packageName} not found on NuGet" });

        var pkg = await _nugetService.GetPackageInfoAsync(packageName);
        return Ok(pkg);
    }

    // Listar todos los paquetes en BD
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var packages = await _db.NugetPackages
            .OrderByDescending(p => p.IsDeprecated)
            .ThenBy(p => p.Name)
            .ToListAsync();
        return Ok(packages);
    }

    // Verificar todos los paquetes del analyzer contra NuGet.org
    [HttpPost("refresh-all")]
    public async Task<IActionResult> RefreshAll()
    {
        var packages = await _db.NugetPackages.ToListAsync();
        var updated = 0;

        foreach (var pkg in packages)
        {
            var success = await _nugetService.CheckAndUpdatePackageAsync(pkg.Name);
            if (success) updated++;
            await Task.Delay(200); // Rate limiting
        }

        return Ok(new { message = "Refresh completado", updated });
    }
}