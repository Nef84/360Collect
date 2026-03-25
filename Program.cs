// ============================================================
//  Program.cs  –  360Collect API  |  DevNef
//  .NET 8 / ASP.NET Core Web API
// ============================================================
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using _360Collect.Data;
using _360Collect.Services;
using HealthChecks.NpgSql;


var builder = WebApplication.CreateBuilder(args);

// ── Base de Datos PostgreSQL ───────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.EnableRetryOnFailure(3))
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);

// ── JWT Authentication ─────────────────────────────────────────
var jwt = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwt["SecretKey"]!);

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.RequireHttpsMetadata = false; // true en produccion
    opt.SaveToken = true;
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(key),
        ValidateIssuer           = true,
        ValidIssuer              = jwt["Issuer"],
        ValidateAudience         = true,
        ValidAudience            = jwt["Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };
    opt.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Auth fallido: {msg}", ctx.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ── Servicios de la aplicacion ─────────────────────────────────
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IIAService, IAServiceSimulado>();
builder.Services.AddScoped<IComunicacionService, ComunicacionServiceStub>();
builder.Services.AddScoped<DatabaseSeeder>();

// ── Controllers ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        opt.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── CORS (desarrollo) ──────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    )
);

// ── Swagger / OpenAPI ──────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "360Collect API",
        Version     = "v1",
        Description = "Sistema Inteligente de Cobranza con IA – DevNef",
        Contact     = new OpenApiContact { Name = "DevNef", Email = "dev@devnef.com" }
    });

    // Boton Authorize en Swagger para probar con JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description  = "JWT Authorization. Ingresa: Bearer {token}",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Agrupar endpoints por controlador
    c.TagActionsBy(api => [api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Other"]);
});

// ── Health checks ──────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!,
               name: "postgresql");

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "360Collect API v1");
        c.RoutePrefix   = string.Empty; // Swagger en raiz: http://localhost:5000
        c.DocumentTitle = "360Collect API – DevNef";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ── Seeder automatico al iniciar ───────────────────────────────
var runSeeder = builder.Configuration.GetValue<bool>("Seeder:RunOnStartup");
if (runSeeder)
{
    using var scope  = app.Services.CreateScope();
    var seeder       = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await seeder.SeedAsync();
        seederLogger.LogInformation("Seeder ejecutado correctamente.");
    }
    catch (Exception ex)
    {
        seederLogger.LogError(ex, "Error al ejecutar el seeder.");
    }
}

app.Run();
