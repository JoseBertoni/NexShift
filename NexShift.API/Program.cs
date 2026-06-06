using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NexShift.Core.Interfaces;
using NexShift.Infrastructure.Data;
using NexShift.Infrastructure.Repositories;
using NexShift.Infrastructure.Services;
using NexShift.Infrastructure.Services.Migrator;
using NexShift.Infrastructure.Services.Migrator.Roslyn;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Servicios base
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Servicios de dominio
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IProjectAnalyzer, ProjectAnalyzer>();
builder.Services.AddScoped<IMigrationService, MigrationService>();
builder.Services.AddScoped<ICodeTransformer, RoslynCodeTransformer>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<INugetService, NugetService>();
builder.Services.AddScoped<IBacklogRuleRepository, BacklogRuleRepository>();
builder.Services.AddScoped<IBacklogDetector, BacklogDetector>();
builder.Services.AddScoped<ITransformationRuleRepository, TransformationRuleRepository>();
builder.Services.AddScoped<IKnownDeprecatedPackageRepository, KnownDeprecatedPackageRepository>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Background jobs: cola + worker
builder.Services.AddSingleton<IMigrationQueue, MigrationQueue>();
builder.Services.AddHostedService<MigrationWorker>();
builder.Services.AddScoped<IBuildValidator, BuildValidator>();

// Authorization con rol Admin
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("role", "admin"));
});

// Base de datos
builder.Services.AddDbContext<NexShiftDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS para el frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// HTTP Client para OAuth
builder.Services.AddHttpClient();

var app = builder.Build();

// Migrar BD automáticamente al arrancar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NexShiftDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();