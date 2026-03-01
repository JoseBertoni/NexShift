using Microsoft.AspNetCore.Mvc;
using NexShift.Infrastructure.Data;

namespace NexShift.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    private readonly NexShiftDbContext _db;
    private readonly ILogger<HealthCheckController> _logger;

    public HealthCheckController(NexShiftDbContext db, ILogger<HealthCheckController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();

            if (!canConnect)
                return StatusCode(503, new { status = "unhealthy", database = "unreachable" });

            _logger.LogInformation("Health check OK");

            return Ok(new
            {
                status = "healthy",
                database = "connected",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }
}