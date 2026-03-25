// ============================================================
//  Data/DatabaseSeeder.cs  –  Seeder completo 360Collect
//  Genera: 20 usuarios, 1000 clientes, cuentas, pagos, promesas,
//  interacciones, predicciones IA y campanas de prueba.
// ============================================================
using Bogus;
using _360Collect.Models;
using Microsoft.EntityFrameworkCore;

namespace _360Collect.Data;

public class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly Random _rng = new(42); // seed fijo para reproducibilidad

    public DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Aplicar migraciones pendientes
        await _db.Database.MigrateAsync();

        // Evitar re-seeding
        if (await _db.Usuarios.AnyAsync())
        {
            _logger.LogInformation("Base de datos ya contiene datos. Seeder omitido.");
            return;
        }

        _logger.LogInformation("Iniciando seeder 360Collect...");

        var usuarios  = await SeedUsuariosAsync();
        var clientes  = await SeedClientesAsync();
        var cuentas   = await SeedCuentasAsync(clientes, usuarios);
        await SeedPagosAsync(cuentas);
        await SeedPromesasAsync(cuentas);
        await SeedInteraccionesAsync(cuentas, usuarios);
        await SeedPrediccionesIAAsync(cuentas);
        await SeedCampanasAsync(usuarios);

        _logger.LogInformation("Seeder completado: {u} usuarios, {c} clientes, {ct} cuentas.",
            usuarios.Count, clientes.Count, cuentas.Count);
    }

    // ── 1. Usuarios ──────────────────────────────────────────
    private async Task<List<Usuario>> SeedUsuariosAsync()
    {
        var lista = new List<(string Nombre, string Email, RolUsuario Rol)>
        {
            ("Carlos Mendez",    "admin@devnef.com",             RolUsuario.Administrador),
            ("Ana Rodriguez",    "ana.rodriguez@devnef.com",     RolUsuario.Administrador),
            ("Luis Flores",      "luis.flores@devnef.com",       RolUsuario.GestorDeCobranza),
            ("Maria Torres",     "maria.torres@devnef.com",      RolUsuario.GestorDeCobranza),
            ("Roberto Diaz",     "roberto.diaz@devnef.com",      RolUsuario.GestorDeCobranza),
            ("Sandra Lopez",     "sandra.lopez@devnef.com",      RolUsuario.AnalistaDeData),
            ("Jorge Ramirez",    "jorge.ramirez@devnef.com",     RolUsuario.AnalistaDeData),
            ("Patricia Vega",    "patricia.vega@devnef.com",     RolUsuario.Supervisor),
            ("Andres Morales",   "andres.morales@devnef.com",    RolUsuario.Supervisor),
            ("Elena Castillo",   "elena.castillo@devnef.com",    RolUsuario.Supervisor),
            ("David Herrera",    "david.herrera@devnef.com",     RolUsuario.Agente),
            ("Carmen Rios",      "carmen.rios@devnef.com",       RolUsuario.Agente),
            ("Miguel Ortiz",     "miguel.ortiz@devnef.com",      RolUsuario.Agente),
            ("Isabel Nunez",     "isabel.nunez@devnef.com",      RolUsuario.Agente),
            ("Fernando Reyes",   "fernando.reyes@devnef.com",    RolUsuario.Agente),
            ("Gabriela Soto",    "gabriela.soto@devnef.com",     RolUsuario.Agente),
            ("Hector Jimenez",   "hector.jimenez@devnef.com",    RolUsuario.Agente),
            ("Valeria Cruz",     "valeria.cruz@devnef.com",      RolUsuario.Agente),
            ("Oscar Medina",     "oscar.medina@devnef.com",      RolUsuario.SoporteTecnico),
            ("Diana Rojas",      "diana.rojas@devnef.com",       RolUsuario.SoporteTecnico),
        };

        var usuarios = lista.Select(u => new Usuario
        {
            Nombre       = u.Nombre,
            Email        = u.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("DevNef2026!"),
            Rol          = u.Rol,
            Activo       = true,
            FechaCreacion = DateTime.UtcNow.AddDays(-_rng.Next(30, 365))
        }).ToList();

        _db.Usuarios.AddRange(usuarios);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} usuarios creados.", usuarios.Count);
        return usuarios;
    }

    // ── 2. Clientes ───────────────────────────────────────────
    private async Task<List<Cliente>> SeedClientesAsync()
    {
        var faker = new Faker<Cliente>("es")
            .RuleFor(c => c.Nombre,          f => f.Name.FullName())
            .RuleFor(c => c.Documento,       f => f.Random.Replace("##########"))
            .RuleFor(c => c.Telefono,        f => f.Phone.PhoneNumber("+(503) ####-####"))
            .RuleFor(c => c.Email,           f => f.Internet.Email())
            .RuleFor(c => c.WhatsApp,        (f, c) => c.Telefono)
            .RuleFor(c => c.CanalPreferido,  f => f.PickRandom<CanalComunicacion>())
            .RuleFor(c => c.Direccion,       f => f.Address.FullAddress())
            .RuleFor(c => c.ScoreRiesgo,     f => Math.Round(f.Random.Double(10, 95), 1))
            .RuleFor(c => c.Activo,          f => f.Random.Bool(0.95f))
            .RuleFor(c => c.FechaRegistro,   f => f.Date.Past(3));

        var clientes = faker.Generate(1000);
        _db.Clientes.AddRange(clientes);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ 1000 clientes creados.");
        return clientes;
    }

    // ── 3. Cuentas ────────────────────────────────────────────
    private async Task<List<Cuenta>> SeedCuentasAsync(List<Cliente> clientes, List<Usuario> usuarios)
    {
        // Distribucion de dias de mora para cada bucket
        var distribucion = new (Bucket Bucket, int Min, int Max, int Cantidad)[]
        {
            (Bucket.PREVENT,  0,   0,   80),
            (Bucket.BK1,      1,   30,  250),
            (Bucket.BK2,      31,  60,  200),
            (Bucket.BK3,      61,  90,  150),
            (Bucket.BK4,      91,  120, 120),
            (Bucket.BK5,      121, 180, 100),
            (Bucket.RECOVERY, 181, 365, 100),
        };

        var agentes = usuarios.Where(u => u.Rol == RolUsuario.Agente).ToList();
        var productos = new[] { "Prestamo Personal", "Tarjeta de Credito", "Prestamo Hipotecario",
                                "Prestamo Vehicular", "Linea de Credito", "Credito PyME" };
        var cuentas = new List<Cuenta>();
        int clienteIdx = 0;

        foreach (var (bucket, min, max, cantidad) in distribucion)
        {
            for (int i = 0; i < cantidad; i++)
            {
                int diasMora = bucket == Bucket.PREVENT ? 0 : _rng.Next(min, max + 1);
                decimal monto = Math.Round((decimal)(_rng.NextDouble() * 49900 + 100), 2);
                decimal pagado = bucket == Bucket.PREVENT ? 0
                    : Math.Round(monto * (decimal)_rng.NextDouble() * 0.4m, 2);

                var cuenta = new Cuenta
                {
                    ClienteId        = clientes[clienteIdx % 1000].Id,
                    Monto            = monto,
                    SaldoPendiente   = monto - pagado,
                    FechaVencimiento = DateTime.UtcNow.AddDays(-diasMora),
                    DiasMora         = diasMora,
                    Bucket           = bucket,
                    Estado           = diasMora > 180 ? EstadoCuenta.EnAgencia : EstadoCuenta.Activa,
                    ScoreIA          = Math.Round(_rng.NextDouble() * 100, 1),
                    NumeroCuenta     = $"DC-{_rng.Next(100000, 999999)}",
                    Producto         = productos[_rng.Next(productos.Length)],
                    AgenteId         = agentes.Count > 0
                                        ? agentes[_rng.Next(agentes.Count)].Id
                                        : (int?)null,
                    FechaCreacion    = DateTime.UtcNow.AddDays(-diasMora - _rng.Next(10, 60)),
                    FechaActualizacion = DateTime.UtcNow
                };

                cuentas.Add(cuenta);
                clienteIdx++;
            }
        }

        _db.Cuentas.AddRange(cuentas);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} cuentas creadas.", cuentas.Count);
        return cuentas;
    }

    // ── 4. Pagos ──────────────────────────────────────────────
    private async Task<int> SeedPagosAsync(List<Cuenta> cuentas)
    {
        var pagos = new List<Pago>();
        var canales = Enum.GetValues<CanalComunicacion>();

        foreach (var cuenta in cuentas)
        {
            // Probabilidad de tener pagos segun bucket
            double prob = cuenta.Bucket switch
            {
                Bucket.PREVENT  => 0.3,
                Bucket.BK1      => 0.6,
                Bucket.BK2      => 0.5,
                Bucket.BK3      => 0.4,
                Bucket.BK4      => 0.3,
                Bucket.BK5      => 0.2,
                Bucket.RECOVERY => 0.1,
                _               => 0.1
            };

            if (_rng.NextDouble() > prob) continue;

            int numPagos = _rng.Next(1, 4);
            for (int i = 0; i < numPagos; i++)
            {
                pagos.Add(new Pago
                {
                    CuentaId     = cuenta.Id,
                    Monto        = Math.Round(cuenta.Monto * (decimal)(_rng.NextDouble() * 0.3 + 0.05), 2),
                    FechaPago    = DateTime.UtcNow.AddDays(-_rng.Next(1, cuenta.DiasMora + 5)),
                    Canal        = canales[_rng.Next(canales.Length)],
                    Estado       = EstadoPago.Aplicado,
                    Referencia   = $"REF-{_rng.Next(1000000, 9999999)}"
                });
            }
        }

        _db.Pagos.AddRange(pagos);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} pagos generados.", pagos.Count);
        return pagos.Count;
    }

    // ── 5. Promesas ───────────────────────────────────────────
    private async Task<int> SeedPromesasAsync(List<Cuenta> cuentas)
    {
        var promesas = new List<Promesa>();

        foreach (var cuenta in cuentas.Where(c => c.DiasMora > 0))
        {
            if (_rng.NextDouble() > 0.45) continue;

            var estadoOpts = new[] { EstadoPromesa.Cumplida, EstadoPromesa.Incumplida, EstadoPromesa.Pendiente };
            promesas.Add(new Promesa
            {
                CuentaId       = cuenta.Id,
                MontoPrometido = Math.Round(cuenta.SaldoPendiente * (decimal)(_rng.NextDouble() * 0.5 + 0.2), 2),
                FechaPromesa   = DateTime.UtcNow.AddDays(_rng.Next(-10, 20)),
                Estado         = estadoOpts[_rng.Next(estadoOpts.Length)],
                Notas          = "Promesa registrada durante gestion de cobro."
            });
        }

        _db.Promesas.AddRange(promesas);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} promesas generadas.", promesas.Count);
        return promesas.Count;
    }

    // ── 6. Interacciones ──────────────────────────────────────
    private async Task<int> SeedInteraccionesAsync(List<Cuenta> cuentas, List<Usuario> usuarios)
    {
        var interacciones = new List<Interaccion>();
        var agentes = usuarios.Where(u => u.Rol == RolUsuario.Agente).ToList();
        var resultados = Enum.GetValues<ResultadoInteraccion>();
        var canales    = Enum.GetValues<CanalComunicacion>();

        foreach (var cuenta in cuentas.Where(c => c.DiasMora > 5))
        {
            int num = _rng.Next(1, Math.Min(cuenta.DiasMora / 10 + 1, 8));
            for (int i = 0; i < num; i++)
            {
                interacciones.Add(new Interaccion
                {
                    CuentaId         = cuenta.Id,
                    UsuarioId        = agentes.Count > 0 ? agentes[_rng.Next(agentes.Count)].Id : (int?)null,
                    Canal            = canales[_rng.Next(canales.Length)],
                    Resultado        = resultados[_rng.Next(resultados.Length)],
                    Notas            = "Gestion de cobro automatica (seeder).",
                    MensajeEnviado   = "Estimado cliente, le recordamos su deuda pendiente.",
                    Fecha            = DateTime.UtcNow.AddDays(-_rng.Next(0, cuenta.DiasMora + 1)),
                    DuracionSegundos = _rng.Next(30, 600)
                });
            }
        }

        _db.Interacciones.AddRange(interacciones);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} interacciones generadas.", interacciones.Count);
        return interacciones.Count;
    }

    // ── 7. Predicciones IA ────────────────────────────────────
    private async Task<int> SeedPrediccionesIAAsync(List<Cuenta> cuentas)
    {
        var predicciones = new List<PrediccionIA>();
        var canales = Enum.GetValues<CanalComunicacion>();
        var horarios = new[] { "08:00-10:00", "10:00-12:00", "14:00-16:00", "17:00-19:00" };

        foreach (var cuenta in cuentas)
        {
            // Score inverso a dias de mora (simulado)
            double score = Math.Max(5, 95 - cuenta.DiasMora * 0.4 + _rng.NextDouble() * 15);
            score = Math.Round(Math.Min(score, 98), 1);

            predicciones.Add(new PrediccionIA
            {
                CuentaId            = cuenta.Id,
                ScorePago           = score,
                ProbabilidadMora    = Math.Round(100 - score + _rng.NextDouble() * 10, 1),
                BucketPredicho      = cuenta.Bucket,
                CanalOptimo         = canales[_rng.Next(canales.Length)],
                HorarioOptimo       = horarios[_rng.Next(horarios.Length)],
                MontoProbablePago   = Math.Round(cuenta.SaldoPendiente * (decimal)(score / 100.0), 2),
                EsAnomalia          = score < 15 && _rng.NextDouble() > 0.85,
                ScoreAnomalia       = Math.Round(_rng.NextDouble() * (score < 20 ? 0.9 : 0.3), 3),
                FechaPrediccion     = DateTime.UtcNow.AddMinutes(-_rng.Next(0, 1440))
            });
        }

        _db.PrediccionesIA.AddRange(predicciones);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} predicciones IA generadas.", predicciones.Count);
        return predicciones.Count;
    }

    // ── 8. Campanas ───────────────────────────────────────────
    private async Task<int> SeedCampanasAsync(List<Usuario> usuarios)
    {
        var gestores = usuarios.Where(u =>
            u.Rol == RolUsuario.GestorDeCobranza || u.Rol == RolUsuario.Administrador).ToList();

        var campanas = new List<Campana>
        {
            new() { Nombre="Campana PREVENT – Mayo 2026",  Descripcion="Recordatorio preventivo pre-corte",
                    BucketObjetivo=Bucket.PREVENT,  Canal=CanalComunicacion.WhatsApp,
                    PlantillaMensaje="Hola {nombre}, le recordamos que su pago de ${monto} vence pronto.",
                    Estado=EstadoCampana.Activa, TotalEnviados=80,  TotalRespondidos=48, TotalPagos=35,
                    FechaInicio=DateTime.UtcNow.AddDays(-5), CreadorId=gestores[0].Id },

            new() { Nombre="Campana BK1 – SMS Masivo",    Descripcion="SMS recordatorio BK1",
                    BucketObjetivo=Bucket.BK1,      Canal=CanalComunicacion.SMS,
                    PlantillaMensaje="DevNef: Tiene un saldo pendiente de ${monto}. Pague hoy.",
                    Estado=EstadoCampana.Activa, TotalEnviados=250, TotalRespondidos=90, TotalPagos=65,
                    FechaInicio=DateTime.UtcNow.AddDays(-10), CreadorId=gestores[0].Id },

            new() { Nombre="Campana BK2 – Email Oferta",  Descripcion="Oferta de plan de pago BK2",
                    BucketObjetivo=Bucket.BK2,      Canal=CanalComunicacion.Email,
                    PlantillaMensaje="Estimado {nombre}, le ofrecemos un plan de pago especial.",
                    Estado=EstadoCampana.Activa, TotalEnviados=200, TotalRespondidos=60, TotalPagos=38,
                    FechaInicio=DateTime.UtcNow.AddDays(-7), CreadorId=gestores[Math.Min(1, gestores.Count-1)].Id },

            new() { Nombre="Campana BK3 – Llamadas",      Descripcion="Gestion telefonica BK3",
                    BucketObjetivo=Bucket.BK3,      Canal=CanalComunicacion.Llamada,
                    PlantillaMensaje="Script: Hola {nombre}, le llamo de DevNef respecto a su cuenta.",
                    Estado=EstadoCampana.Pausada, TotalEnviados=100, TotalRespondidos=40, TotalPagos=20,
                    FechaInicio=DateTime.UtcNow.AddDays(-15), CreadorId=gestores[0].Id },

            new() { Nombre="Campana RECOVERY – Ultima Oportunidad", Descripcion="Oferta final antes de agencia externa",
                    BucketObjetivo=Bucket.RECOVERY, Canal=CanalComunicacion.WhatsApp,
                    PlantillaMensaje="Esta es su ultima oportunidad para regularizar su cuenta con DevNef.",
                    Estado=EstadoCampana.Borrador, TotalEnviados=0, TotalRespondidos=0, TotalPagos=0,
                    CreadorId=gestores[0].Id },
        };

        _db.Campanas.AddRange(campanas);
        await _db.SaveChangesAsync();
        _logger.LogInformation("  ✓ {n} campanas generadas.", campanas.Count);
        return campanas.Count;
    }
}
