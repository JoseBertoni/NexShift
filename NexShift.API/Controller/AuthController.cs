using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;
using NexShift.Infrastructure.Services;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NexShift.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly NexShiftDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly HttpClient _httpClient;

    public AuthController(NexShiftDbContext db, IJwtService jwtService, IConfiguration configuration, ILogger<AuthController> logger, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _jwtService = jwtService;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    // GET /api/auth/github → redirige a GitHub
    [HttpGet("github")]
    public IActionResult GitHubLogin()
    {
        var clientId = _configuration["GitHub:ClientId"];
        var redirectUrl = $"https://github.com/login/oauth/authorize?client_id={clientId}&scope=user:email";
        return Redirect(redirectUrl);
    }

    // GET /api/auth/github/callback → recibe code, obtiene token, crea/actualiza user
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Code no recibido" });

        try
        {
            // 1. Intercambiar code por access token
            var clientId = _configuration["GitHub:ClientId"];
            var clientSecret = _configuration["GitHub:ClientSecret"];

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            tokenRequest.Content = JsonContent.Create(new { client_id = clientId, client_secret = clientSecret, code });

            var tokenResponse = await _httpClient.SendAsync(tokenRequest);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            if (!tokenData.TryGetProperty("access_token", out var accessTokenElement))
                return BadRequest(new { error = "No se pudo obtener el access token de GitHub" });

            var accessToken = accessTokenElement.GetString()!;

            // 2. Obtener datos del usuario de GitHub
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            userRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("NexShift", "1.0"));

            var userResponse = await _httpClient.SendAsync(userRequest);
            var userJson = await userResponse.Content.ReadAsStringAsync();
            var githubUser = JsonSerializer.Deserialize<JsonElement>(userJson);

            var githubId = githubUser.GetProperty("id").GetInt64();
            var login = githubUser.GetProperty("login").GetString()!;
            var name = githubUser.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var avatarUrl = githubUser.TryGetProperty("avatar_url", out var avatarEl) ? avatarEl.GetString() : null;

            // 3. Obtener email si no viene en el perfil
            string? email = null;
            if (githubUser.TryGetProperty("email", out var emailEl) && emailEl.ValueKind != JsonValueKind.Null)
            {
                email = emailEl.GetString();
            }
            else
            {
                var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                emailRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("NexShift", "1.0"));

                var emailResponse = await _httpClient.SendAsync(emailRequest);
                var emailJson = await emailResponse.Content.ReadAsStringAsync();
                var emails = JsonSerializer.Deserialize<JsonElement>(emailJson);

                foreach (var e in emails.EnumerateArray())
                {
                    if (e.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                    {
                        email = e.GetProperty("email").GetString();
                        break;
                    }
                }
            }

            // 4. Crear o actualizar usuario en BD
            var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    GitHubId = githubId,
                    Login = login,
                    Name = name,
                    Email = email,
                    AvatarUrl = avatarUrl,
                    GitHubAccessToken = accessToken,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };
                _db.Users.Add(user);
                _logger.LogInformation("Nuevo usuario registrado: {Login}", login);
            }
            else
            {
                user.Login = login;
                user.Name = name;
                user.Email = email;
                user.AvatarUrl = avatarUrl;
                user.GitHubAccessToken = accessToken;
                user.LastLoginAt = DateTime.UtcNow;
                _logger.LogInformation("Usuario actualizado: {Login}", login);
            }

            await _db.SaveChangesAsync();

            // 5. Generar JWT
            var jwt = _jwtService.GenerateToken(user);

            // 6. Redirigir al frontend con el token
            var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
            return Redirect($"{frontendUrl}/auth/callback?token={jwt}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GitHub OAuth callback");
            return StatusCode(500, new { error = "Error durante la autenticación" });
        }
    }

    // GET /api/auth/me → devuelve el usuario actual
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var token = Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();

        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Token no proporcionado" });

        var userId = _jwtService.ValidateToken(token);
        if (userId == null)
            return Unauthorized(new { error = "Token inválido" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { error = "Usuario no encontrado" });

        return Ok(new
        {
            id = user.Id,
            login = user.Login,
            name = user.Name,
            email = user.Email,
            avatarUrl = user.AvatarUrl,
            plan = user.Plan.ToString(),
            reposUsed = user.ReposUsed,
            createdAt = user.CreatedAt
        });
    }
}