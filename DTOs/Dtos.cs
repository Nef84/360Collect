// ============================================================
//  DTOs/RequestDtos.cs  –  Data Transfer Objects de entrada
// ============================================================
using System.ComponentModel.DataAnnotations;
using _360Collect.Models;

namespace _360Collect.DTOs;

// ── Auth ──────────────────────────────────────────────────────
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record LoginResponse(
    string Token,
    string Nombre,
    string Email,
    string Rol,
    DateTime Expiracion
);

// ── Cliente ───────────────────────────────────────────────────
public record CrearClienteRequest(
    [Required, MaxLength(150)] string Nombre,
    [MaxLength(20)]  string? Documento,
    [MaxLength(20)]  string? Telefono,
    [EmailAddress, MaxLength(150)] string? Email,
    [MaxLength(20)]  string? WhatsApp,
    CanalComunicacion CanalPreferido,
    [MaxLength(200)] string? Direccion
);

public record ActualizarClienteRequest(
    [MaxLength(150)] string? Nombre,
    [MaxLength(20)]  string? Telefono,
    [EmailAddress, MaxLength(150)] string? Email,
    [MaxLength(20)]  string? WhatsApp,
    CanalComunicacion? CanalPreferido,
    [MaxLength(200)] string? Direccion
);

public record ClienteDto(
    int Id, string Nombre, string? Documento, string? Telefono,
    string? Email, string? WhatsApp, string CanalPreferido,
    double ScoreRiesgo, bool Activo, DateTime FechaRegistro,
    int TotalCuentas, decimal TotalDeuda
);

// ── Cuenta ────────────────────────────────────────────────────
public record CrearCuentaRequest(
    [Required] int ClienteId,
    [Required, Range(1, 9999999)] decimal Monto,
    [Required] DateTime FechaVencimiento,
    [MaxLength(50)]  string? NumeroCuenta,
    [MaxLength(100)] string? Producto,
    int? AgenteId
);

public record ActualizarCuentaRequest(
    EstadoCuenta? Estado,
    int? AgenteId,
    decimal? SaldoPendiente
);

public record CuentaDto(
    int Id, int ClienteId, string NombreCliente,
    decimal Monto, decimal SaldoPendiente,
    DateTime FechaVencimiento, int DiasMora,
    string Bucket, string Estado,
    double ScoreIA, string? NumeroCuenta, string? Producto,
    string? NombreAgente, DateTime FechaCreacion
);

// ── Pago ──────────────────────────────────────────────────────
public record RegistrarPagoRequest(
    [Required] int CuentaId,
    [Required, Range(0.01, 9999999)] decimal Monto,
    [Required] DateTime FechaPago,
    CanalComunicacion Canal,
    string? Referencia
);

// ── Promesa ───────────────────────────────────────────────────
public record RegistrarPromesaRequest(
    [Required] int CuentaId,
    [Required, Range(0.01, 9999999)] decimal MontoPrometido,
    [Required] DateTime FechaPromesa,
    string? Notas
);

// ── Campana ───────────────────────────────────────────────────
public record CrearCampanaRequest(
    [Required, MaxLength(150)] string Nombre,
    [MaxLength(500)] string? Descripcion,
    [Required] Bucket BucketObjetivo,
    [Required] CanalComunicacion Canal,
    [MaxLength(1000)] string? PlantillaMensaje,
    DateTime? FechaInicio,
    DateTime? FechaFin
);

// ── Usuario ───────────────────────────────────────────────────
public record CrearUsuarioRequest(
    [Required, MaxLength(100)] string Nombre,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] RolUsuario Rol
);

public record CambiarPasswordRequest(
    [Required] string PasswordActual,
    [Required, MinLength(8)] string NuevoPassword
);

public record UsuarioDto(
    int Id, string Nombre, string Email, string Rol,
    bool Activo, DateTime FechaCreacion, DateTime? UltimoAcceso
);

// ── Interaccion ───────────────────────────────────────────────
public record RegistrarInteraccionRequest(
    [Required] int CuentaId,
    [Required] CanalComunicacion Canal,
    [Required] ResultadoInteraccion Resultado,
    string? Notas,
    int DuracionSegundos = 0
);

// ── Reportes / KPIs ───────────────────────────────────────────
public record KpiGlobalDto(
    int TotalClientes, int TotalCuentas, decimal TotalCartera,
    decimal TotalRecuperado, double PorcentajeRecuperacion,
    ResumenBucketDto[] ResumenBuckets,
    int PromesasPendientes, int PromesasCumplidas, int PromesasIncumplidas,
    ContactabilidadDto[] Contactabilidad,
    int AnomaliasMes
);

public record ResumenBucketDto(
    string Bucket, int CantidadCuentas, decimal MontoTotal,
    double PorcentajeCartera, double TasaRecuperacion
);

public record ContactabilidadDto(
    string Canal, int TotalInteracciones, int Contactados,
    double TasaContactabilidad
);

public record ProductividadAgenteDto(
    int AgenteId, string NombreAgente,
    int CuentasGestionadas, int PagosObtenidos,
    decimal MontoRecuperado, double TasaExito
);

// ── Filtros / Paginacion ──────────────────────────────────────
public record FiltroClienteRequest(
    string? Busqueda = null,
    bool? Activo = null,
    int Pagina = 1,
    int TamanoPagina = 20
);

public record FiltroCuentaRequest(
    Bucket? Bucket = null,
    EstadoCuenta? Estado = null,
    int? AgenteId = null,
    int? ClienteId = null,
    decimal? MontoMin = null,
    decimal? MontoMax = null,
    int Pagina = 1,
    int TamanoPagina = 20
);

public record PaginatedResult<T>(
    IEnumerable<T> Items,
    int TotalItems,
    int Pagina,
    int TamanoPagina,
    int TotalPaginas
);
