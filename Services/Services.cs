// ============================================================
//  Services.cs – Servicios principales de 360Collect
// ============================================================
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using _360Collect.Models;

namespace _360Collect.Services
{
    // ============================================================
    //  JwtService – Generación de tokens JWT
    // ============================================================
    public interface IJwtService
    {
        string GenerarToken(Usuario usuario);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config) => _config = config;

        public string GenerarToken(Usuario usuario)
        {
            var jwt = _config.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.UtcNow.AddHours(double.Parse(jwt["ExpirationHours"]!));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(ClaimTypes.Name,               usuario.Nombre),
                new Claim(ClaimTypes.Role,               usuario.Rol.ToString()),
                new Claim("rol_id",                      ((int)usuario.Rol).ToString()),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // ============================================================
    //  BucketService – Lógica de segmentación
    // ============================================================
    public static class BucketService
    {
        public static Models.Bucket AsignarBucket(int diasMora) => diasMora switch
        {
            0 => Models.Bucket.PREVENT,
            <= 30 => Models.Bucket.BK1,
            <= 60 => Models.Bucket.BK2,
            <= 90 => Models.Bucket.BK3,
            <= 120 => Models.Bucket.BK4,
            <= 180 => Models.Bucket.BK5,
            _ => Models.Bucket.RECOVERY
        };

        public static int CalcularDiasMora(DateTime fechaVencimiento)
            => Math.Max(0, (DateTime.UtcNow.Date - fechaVencimiento.Date).Days);
    }

    // ============================================================
    //  IAService – Simulación de predicciones IA
    // ============================================================
    public interface IIAService
    {
        PrediccionIA GenerarPrediccion(Cuenta cuenta);
        IEnumerable<PrediccionIA> DetectarAnomalias(IEnumerable<Cuenta> cuentas);
    }

    public class IAServiceSimulado : IIAService
    {
        private readonly Random _rng = new();
        private readonly string[] _horarios = ["08:00-10:00", "10:00-12:00", "14:00-16:00", "17:00-19:00"];

        public PrediccionIA GenerarPrediccion(Cuenta cuenta)
        {
            double score = Math.Max(5, 95 - cuenta.DiasMora * 0.4 + _rng.NextDouble() * 15);
            score = Math.Min(score, 98);
            var canalOptimo = cuenta.DiasMora switch
            {
                0 => CanalComunicacion.SMS,
                <= 30 => CanalComunicacion.WhatsApp,
                <= 90 => CanalComunicacion.Llamada,
                _ => CanalComunicacion.Email
            };

            return new PrediccionIA
            {
                CuentaId = cuenta.Id,
                ScorePago = Math.Round(score, 1),
                ProbabilidadMora = Math.Round(100 - score + _rng.NextDouble() * 10, 1),
                BucketPredicho = BucketService.AsignarBucket(cuenta.DiasMora + _rng.Next(-5, 10)),
                CanalOptimo = canalOptimo,
                HorarioOptimo = _horarios[_rng.Next(_horarios.Length)],
                MontoProbablePago = Math.Round(cuenta.SaldoPendiente * (decimal)(score / 100.0), 2),
                EsAnomalia = false,
                ScoreAnomalia = Math.Round(_rng.NextDouble() * 0.3, 3),
                FechaPrediccion = DateTime.UtcNow
            };
        }

        public IEnumerable<PrediccionIA> DetectarAnomalias(IEnumerable<Cuenta> cuentas)
            => cuentas
               .Where(c => c.DiasMora > 150 && c.SaldoPendiente > 10000)
               .Select(c =>
               {
                   var p = GenerarPrediccion(c);
                   p.EsAnomalia = true;
                   p.ScoreAnomalia = Math.Round(0.7 + _rng.NextDouble() * 0.29, 3);
                   return p;
               });
    }

    // ============================================================
    //  ComunicacionService – Canal omnicanal stub
    // ============================================================
    public interface IComunicacionService
    {
        Task<bool> EnviarWhatsAppAsync(string numero, string mensaje);
        Task<bool> EnviarSMSAsync(string numero, string mensaje);
        Task<bool> EnviarEmailAsync(string email, string asunto, string cuerpo);
        Task<bool> IniciarLlamadaAsync(string numero, string script);
    }

    public class ComunicacionServiceStub : IComunicacionService
    {
        private readonly ILogger<ComunicacionServiceStub> _logger;
        private readonly IConfiguration _config;

        public ComunicacionServiceStub(ILogger<ComunicacionServiceStub> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public Task<bool> EnviarWhatsAppAsync(string numero, string mensaje)
        {
            _logger.LogInformation("[STUB-WhatsApp] → {num}: {msg}", numero, mensaje);
            return Task.FromResult(true);
        }

        public Task<bool> EnviarSMSAsync(string numero, string mensaje)
        {
            _logger.LogInformation("[STUB-SMS] → {num}: {msg}", numero, mensaje);
            return Task.FromResult(true);
        }

        public Task<bool> EnviarEmailAsync(string email, string asunto, string cuerpo)
        {
            _logger.LogInformation("[STUB-Email] → {email} | Asunto: {asunto}", email, asunto);
            return Task.FromResult(true);
        }

        public Task<bool> IniciarLlamadaAsync(string numero, string script)
        {
            _logger.LogInformation("[STUB-Llamada] → {num} | Script: {script}", numero, script);
            return Task.FromResult(true);
        }
    }
}