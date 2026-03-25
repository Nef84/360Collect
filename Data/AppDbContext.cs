// ============================================================
//  Data/AppDbContext.cs  –  EF Core DbContext
// ============================================================
using Microsoft.EntityFrameworkCore;
using _360Collect.Models;

namespace _360Collect.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario>       Usuarios       { get; set; }
    public DbSet<Cliente>       Clientes       { get; set; }
    public DbSet<Cuenta>        Cuentas        { get; set; }
    public DbSet<BucketHistorial> BucketHistorial { get; set; }
    public DbSet<Pago>          Pagos          { get; set; }
    public DbSet<Promesa>       Promesas       { get; set; }
    public DbSet<Interaccion>   Interacciones  { get; set; }
    public DbSet<Campana>       Campanas       { get; set; }
    public DbSet<PrediccionIA>  PrediccionesIA { get; set; }
    public DbSet<AuditLog>      AuditLogs      { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indices de rendimiento
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Documento).HasFilter("\"Documento\" IS NOT NULL");

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<Cuenta>()
            .HasIndex(c => c.Bucket);

        modelBuilder.Entity<Cuenta>()
            .HasIndex(c => c.DiasMora);

        modelBuilder.Entity<Cuenta>()
            .HasIndex(c => c.Estado);

        modelBuilder.Entity<Interaccion>()
            .HasIndex(i => i.Fecha);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Fecha);

        // Nombre de tablas en snake_case (PostgreSQL convention)
        modelBuilder.Entity<Usuario>().ToTable("usuarios");
        modelBuilder.Entity<Cliente>().ToTable("clientes");
        modelBuilder.Entity<Cuenta>().ToTable("cuentas");
        modelBuilder.Entity<BucketHistorial>().ToTable("bucket_historial");
        modelBuilder.Entity<Pago>().ToTable("pagos");
        modelBuilder.Entity<Promesa>().ToTable("promesas");
        modelBuilder.Entity<Interaccion>().ToTable("interacciones");
        modelBuilder.Entity<Campana>().ToTable("campanas");
        modelBuilder.Entity<PrediccionIA>().ToTable("predicciones_ia");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");

        // Relacion Cuenta -> Agente (opcional, no cascade delete)
        modelBuilder.Entity<Cuenta>()
            .HasOne(c => c.Agente)
            .WithMany()
            .HasForeignKey(c => c.AgenteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relacion Interaccion -> Usuario (opcional)
        modelBuilder.Entity<Interaccion>()
            .HasOne(i => i.Usuario)
            .WithMany(u => u.Interacciones)
            .HasForeignKey(i => i.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relacion AuditLog -> Usuario (opcional)
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Usuario)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
