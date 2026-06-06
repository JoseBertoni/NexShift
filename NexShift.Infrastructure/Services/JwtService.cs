using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NexShift.Core.Entities;
using NexShift.Core.Interfaces;

namespace NexShift.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly string _secret;
        private readonly int _expirationDays;

        public JwtService(IConfiguration configuration)
        {
            _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret no configurado");
            _expirationDays = int.Parse(configuration["Jwt:ExpirationDays"] ?? "30");
        }

        public string GenerateToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim("userId", user.Id.ToString()),
            new Claim("login", user.Login),
            new Claim("plan", user.Plan.ToString()),
            new Claim("role", user.IsAdmin ? "admin" : "user"),
        };

            var token = new JwtSecurityToken(
                issuer: "nexshift",
                audience: "nexshift",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(_expirationDays),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Guid? ValidateToken(string token)
        {
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
                var handler = new JwtSecurityTokenHandler();

                handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = "nexshift",
                    ValidateAudience = true,
                    ValidAudience = "nexshift",
                    ValidateLifetime = true
                }, out var validatedToken);

                var jwt = (JwtSecurityToken)validatedToken;
                var userId = jwt.Claims.First(c => c.Type == "userId").Value;
                return Guid.Parse(userId);
            }
            catch
            {
                return null;
            }
        }
    }
}
