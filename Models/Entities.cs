// ============================================================
//  Models/Entities.cs  –  Entidades del dominio 360Collect
// ============================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _360Collect.Models;

// ── Usuario ──────────────────────────────────────────────────
public class Usuario
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string Nombre { get; set; } = "";
    [Required, MaxLength(150)] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public RolUsuario Rol { get; set; } = RolUsuario.Agente;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAcceso { get; set; }

    // Navegacion
    public ICollection<Interaccion> Interacciones { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}

// ── Cliente ───────────────────────────────────────────────────
public class Cliente
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(150)] public string Nombre { get; set; } = "";
    [MaxLength(20)]  public string? Documento { get; set; }
    [MaxLength(20)]  public string? Telefono { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(20)]  public string? WhatsApp { get; set; }
    public CanalComunicacion CanalPreferido { get; set; } = CanalComunicacion.WhatsApp;
    [MaxLength(200)] public string? Direccion { get; set; }
    public double ScoreRiesgo { get; set; } = 50.0;
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Navegacion
    public ICollection<Cuenta> Cuentas { get; set; } = [];
}

// ── Cuenta ────────────────────────────────────────────────────
public class Cuenta
{
    [Key] public int Id { get; set; }
    public int ClienteId { get; set; }
    [ForeignKey(nameof(ClienteId))] public Cliente Cliente { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")] public decimal Monto { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SaldoPendiente { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int DiasMora { get; set; }
    public Bucket Bucket { get; set; } = Bucket.PREVENT;
    public EstadoCuenta Estado { get; set; } = EstadoCuenta.Activa;
    public double ScoreIA { get; set; } = 50.0;

    [MaxLength(50)] public string? NumeroCuenta { get; set; }
    [MaxLength(100)] public string? Producto { get; set; }

    public int? AgenteId { get; set; }
    [ForeignKey(nameof(AgenteId))] public Usuario? Agente { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    // Navegacion
    public ICollection<Pago> Pagos { get; set; } = [];
    public ICollection<Promesa> Promesas { get; set; } = [];
    public ICollection<Interaccion> Interacciones { get; set; } = [];
    public ICollection<BucketHistorial> BucketHistorial { get; set; } = [];
    public ICollection<PrediccionIA> Predicciones { get; set; } = [];
}

// ── BucketHistorial ───────────────────────────────────────────
public class BucketHistorial
{
    [Key] public int Id { get; set; }
    public int CuentaId { get; set; }
    [ForeignKey(nameof(CuentaId))] public Cuenta Cuenta { get; set; } = null!;
    public Bucket BucketAnterior { get; set; }
    public Bucket BucketNuevo { get; set; }
    [MaxLength(200)] public string? Motivo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

// ── Pago ──────────────────────────────────────────────────────
public class Pago
{
    [Key] public int Id { get; set; }
    public int CuentaId { get; set; }
    [ForeignKey(nameof(CuentaId))] public Cuenta Cuenta { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; }
    public CanalComunicacion Canal { get; set; }
    public EstadoPago Estado { get; set; } = EstadoPago.Aplicado;
    [MaxLength(200)] public string? Referencia { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

// ── Promesa ───────────────────────────────────────────────────
public class Promesa
{
    [Key] public int Id { get; set; }
    public int CuentaId { get; set; }
    [ForeignKey(nameof(CuentaId))] public Cuenta Cuenta { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal MontoPrometido { get; set; }
    public DateTime FechaPromesa { get; set; }
    public EstadoPromesa Estado { get; set; } = EstadoPromesa.Pendiente;
    [MaxLength(300)] public string? Notas { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

// ── Interaccion ───────────────────────────────────────────────
public class Interaccion
{
    [Key] public int Id { get; set; }
    public int CuentaId { get; set; }
    [ForeignKey(nameof(CuentaId))] public Cuenta Cuenta { get; set; } = null!;
    public int? UsuarioId { get; set; }
    [ForeignKey(nameof(UsuarioId))] public Usuario? Usuario { get; set; }
    public CanalComunicacion Canal { get; set; }
    public ResultadoInteraccion Resultado { get; set; }
    [MaxLength(500)] public string? Notas { get; set; }
    [MaxLength(500)] public string? MensajeEnviado { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int DuracionSegundos { get; set; }
}

// ── Campana ───────────────────────────────────────────────────
public class Campana
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(150)] public string Nombre { get; set; } = "";
    [MaxLength(500)] public string? Descripcion { get; set; }
    public Bucket BucketObjetivo { get; set; }
    public CanalComunicacion Canal { get; set; }
    [MaxLength(1000)] public string? PlantillaMensaje { get; set; }
    public EstadoCampana Estado { get; set; } = EstadoCampana.Borrador;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int TotalEnviados { get; set; }
    public int TotalRespondidos { get; set; }
    public int TotalPagos { get; set; }
    public int CreadorId { get; set; }
    [ForeignKey(nameof(CreadorId))] public Usuario Creador { get; set; } = null!;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

// ── PrediccionIA ──────────────────────────────────────────────
public class PrediccionIA
{
    [Key] public int Id { get; set; }
    public int CuentaId { get; set; }
    [ForeignKey(nameof(CuentaId))] public Cuenta Cuenta { get; set; } = null!;
    public double ScorePago { get; set; }
    public double ProbabilidadMora { get; set; }
    public Bucket BucketPredicho { get; set; }
    public CanalComunicacion CanalOptimo { get; set; }
    public string? HorarioOptimo { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MontoProbablePago { get; set; }
    public bool EsAnomalia { get; set; }
    public double ScoreAnomalia { get; set; }
    public DateTime FechaPrediccion { get; set; } = DateTime.UtcNow;
}

// ── AuditLog ──────────────────────────────────────────────────
public class AuditLog
{
    [Key] public int Id { get; set; }
    public int? UsuarioId { get; set; }
    [ForeignKey(nameof(UsuarioId))] public Usuario? Usuario { get; set; }
    [MaxLength(100)] public string Accion { get; set; } = "";
    [MaxLength(100)] public string Entidad { get; set; } = "";
    public int? EntidadId { get; set; }
    [MaxLength(1000)] public string? Detalle { get; set; }
    [MaxLength(50)] public string? IP { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
