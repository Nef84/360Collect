// ============================================================
//  Controllers/CampanasController.cs
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _360Collect.Data;
using _360Collect.DTOs;
using _360Collect.Models;
using _360Collect.Services;

namespace _360Collect.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CampanasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IComunicacionService _comunicacion;
    private readonly ILogger<CampanasController> _logger;

    public CampanasController(AppDbContext db, IComunicacionService comunicacion,
        ILogger<CampanasController> logger)
    {
        _db = db;
        _comunicacion = comunicacion;
        _logger = logger;
    }

    // GET api/campanas
    [HttpGet]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor")]
    public async Task<IActionResult> GetAll([FromQuery] EstadoCampana? estado = null)
    {
        var q = _db.Campanas.Include(c => c.Creador).AsNoTracking();
        if (estado.HasValue) q = q.Where(c => c.Estado == estado.Value);
        return Ok(await q.OrderByDescending(c => c.FechaCreacion).ToListAsync());
    }

    // GET api/campanas/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Campanas.Include(x => x.Creador)
                    .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { mensaje = "Campana no encontrada." });
        return Ok(c);
    }

    // POST api/campanas
    [HttpPost]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    public async Task<IActionResult> Crear([FromBody] CrearCampanaRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var campana = new Campana
        {
            Nombre           = req.Nombre,
            Descripcion      = req.Descripcion,
            BucketObjetivo   = req.BucketObjetivo,
            Canal            = req.Canal,
            PlantillaMensaje = req.PlantillaMensaje,
            Estado           = EstadoCampana.Borrador,
            FechaInicio      = req.FechaInicio,
            FechaFin         = req.FechaFin,
            CreadorId        = usuarioId
        };

        _db.Campanas.Add(campana);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = campana.Id }, campana);
    }

    // PUT api/campanas/{id}/estado
    [HttpPut("{id:int}/estado")]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoCampana nuevoEstado)
    {
        var campana = await _db.Campanas.FindAsync(id);
        if (campana is null) return NotFound(new { mensaje = "Campana no encontrada." });
        campana.Estado = nuevoEstado;
        if (nuevoEstado == EstadoCampana.Activa && campana.FechaInicio is null)
            campana.FechaInicio = DateTime.UtcNow;
        if (nuevoEstado == EstadoCampana.Finalizada)
            campana.FechaFin = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(campana);
    }

    // POST api/campanas/{id}/ejecutar (simulacion)
    [HttpPost("{id:int}/ejecutar")]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    public async Task<IActionResult> Ejecutar(int id)
    {
        var campana = await _db.Campanas.FindAsync(id);
        if (campana is null) return NotFound(new { mensaje = "Campana no encontrada." });
        if (campana.Estado != EstadoCampana.Activa)
            return BadRequest(new { mensaje = "La campana debe estar Activa para ejecutarse." });

        // Obtener cuentas del bucket objetivo
        var cuentas = await _db.Cuentas
            .Where(c => c.Bucket == campana.BucketObjetivo && c.Estado == EstadoCuenta.Activa)
            .Include(c => c.Cliente)
            .Take(500) // limite de seguridad
            .ToListAsync();

        int enviados = 0;
        foreach (var cuenta in cuentas)
        {
            if (cuenta.Cliente is null) continue;
            var mensaje = (campana.PlantillaMensaje ?? "Recordatorio de pago DevNef")
                .Replace("{nombre}", cuenta.Cliente.Nombre)
                .Replace("{monto}",  cuenta.SaldoPendiente.ToString("N2"));

            bool ok = campana.Canal switch
            {
                CanalComunicacion.WhatsApp => await _comunicacion.EnviarWhatsAppAsync(
                    cuenta.Cliente.WhatsApp ?? cuenta.Cliente.Telefono ?? "", mensaje),
                CanalComunicacion.SMS      => await _comunicacion.EnviarSMSAsync(
                    cuenta.Cliente.Telefono ?? "", mensaje),
                CanalComunicacion.Email    => await _comunicacion.EnviarEmailAsync(
                    cuenta.Cliente.Email ?? "", $"Aviso de cobro – DevNef", mensaje),
                CanalComunicacion.Llamada  => await _comunicacion.IniciarLlamadaAsync(
                    cuenta.Cliente.Telefono ?? "", mensaje),
                _ => false
            };

            if (ok) enviados++;
        }

        campana.TotalEnviados += enviados;
        await _db.SaveChangesAsync();

        return Ok(new {
            campanaId  = id,
            enviados,
            totalCuentas = cuentas.Count,
            canal      = campana.Canal.ToString()
        });
    }

    // GET api/campanas/{id}/estadisticas
    [HttpGet("{id:int}/estadisticas")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor,AnalistaDeData")]
    public async Task<IActionResult> GetEstadisticas(int id)
    {
        var campana = await _db.Campanas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (campana is null) return NotFound(new { mensaje = "Campana no encontrada." });

        double tasa = campana.TotalEnviados > 0
            ? Math.Round((double)campana.TotalRespondidos / campana.TotalEnviados * 100, 1)
            : 0;

        return Ok(new
        {
            campana.Id,
            campana.Nombre,
            campana.TotalEnviados,
            campana.TotalRespondidos,
            campana.TotalPagos,
            TasaRespuesta   = tasa,
            TasaConversion  = campana.TotalEnviados > 0
                ? Math.Round((double)campana.TotalPagos / campana.TotalEnviados * 100, 1) : 0
        });
    }
}

// ============================================================
//  Controllers/ReportesController.cs  –  KPIs y metricas
// ============================================================


[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportesController(AppDbContext db) => _db = db;

    // GET api/reportes/kpis
    [HttpGet("kpis")]
    [Authorize(Roles = "Administrador,Supervisor,AnalistaDeData")]
    public async Task<IActionResult> GetKpis()
    {
        var totalClientes  = await _db.Clientes.CountAsync(c => c.Activo);
        var totalCuentas   = await _db.Cuentas.CountAsync();
        var totalCartera   = await _db.Cuentas.SumAsync(c => c.SaldoPendiente);
        var totalRecuperado = await _db.Pagos
            .Where(p => p.Estado == EstadoPago.Aplicado)
            .SumAsync(p => p.Monto);

        // Resumen por bucket
        var buckets = await _db.Cuentas
            .GroupBy(c => c.Bucket)
            .Select(g => new {
                Bucket    = g.Key,
                Cantidad  = g.Count(),
                MontoTotal = g.Sum(c => c.SaldoPendiente)
            })
            .ToListAsync();

        var totalCarteraTotal = await _db.Cuentas.SumAsync(c => c.Monto);
        var resumenBuckets = buckets.Select(b => new ResumenBucketDto(
            b.Bucket.ToString(),
            b.Cantidad,
            b.MontoTotal,
            totalCarteraTotal > 0
                ? Math.Round((double)b.MontoTotal / (double)totalCarteraTotal * 100, 1)
                : 0,
            b.Cantidad > 0 ? Math.Round(_db.Pagos
                .Count(p => p.Cuenta.Bucket == b.Bucket) * 100.0 / b.Cantidad, 1) : 0
        )).ToArray();

        // Promesas
        var promesasPendientes  = await _db.Promesas.CountAsync(p => p.Estado == EstadoPromesa.Pendiente);
        var promesasCumplidas   = await _db.Promesas.CountAsync(p => p.Estado == EstadoPromesa.Cumplida);
        var promesasIncumplidas = await _db.Promesas.CountAsync(p => p.Estado == EstadoPromesa.Incumplida);

        // Contactabilidad por canal
        var contactabilidad = await _db.Interacciones
            .GroupBy(i => i.Canal)
            .Select(g => new {
                Canal        = g.Key,
                Total        = g.Count(),
                Contactados  = g.Count(i => i.Resultado == ResultadoInteraccion.Contactado ||
                                            i.Resultado == ResultadoInteraccion.PromesaDePago ||
                                            i.Resultado == ResultadoInteraccion.PagoTotal)
            })
            .ToListAsync();

        var contactabilidadDto = contactabilidad.Select(c => new ContactabilidadDto(
            c.Canal.ToString(), c.Total, c.Contactados,
            c.Total > 0 ? Math.Round((double)c.Contactados / c.Total * 100, 1) : 0
        )).ToArray();

        // Anomalias
        var anomaliasMes = await _db.PrediccionesIA
            .CountAsync(p => p.EsAnomalia && p.FechaPrediccion >= DateTime.UtcNow.AddDays(-30));

        return Ok(new KpiGlobalDto(
            totalClientes, totalCuentas, totalCartera, totalRecuperado,
            totalCartera > 0 ? Math.Round((double)totalRecuperado / (double)totalCartera * 100, 2) : 0,
            resumenBuckets,
            promesasPendientes, promesasCumplidas, promesasIncumplidas,
            contactabilidadDto, anomaliasMes
        ));
    }

    // GET api/reportes/productividad-agentes
    [HttpGet("productividad-agentes")]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> GetProductividadAgentes()
    {
        var agentes = await _db.Usuarios
            .Where(u => u.Rol == RolUsuario.Agente && u.Activo)
            .AsNoTracking()
            .ToListAsync();

        var resultado = new List<ProductividadAgenteDto>();

        foreach (var agente in agentes)
        {
            var cuentas = await _db.Cuentas.CountAsync(c => c.AgenteId == agente.Id);
            var pagos   = await _db.Pagos
                .Where(p => p.Cuenta.AgenteId == agente.Id && p.Estado == EstadoPago.Aplicado)
                .ToListAsync();

            resultado.Add(new ProductividadAgenteDto(
                agente.Id, agente.Nombre, cuentas,
                pagos.Count,
                pagos.Sum(p => p.Monto),
                cuentas > 0 ? Math.Round((double)pagos.Count / cuentas * 100, 1) : 0
            ));
        }

        return Ok(resultado.OrderByDescending(r => r.MontoRecuperado));
    }

    // GET api/reportes/distribucion-buckets
    [HttpGet("distribucion-buckets")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor,AnalistaDeData")]
    public async Task<IActionResult> GetDistribucionBuckets()
    {
        var dist = await _db.Cuentas
            .GroupBy(c => c.Bucket)
            .Select(g => new {
                Bucket     = g.Key.ToString(),
                Cantidad   = g.Count(),
                Monto      = g.Sum(c => c.SaldoPendiente),
                PromedioMora = g.Average(c => c.DiasMora)
            })
            .OrderBy(x => x.Bucket)
            .ToListAsync();

        return Ok(dist);
    }

    // GET api/reportes/anomalias
    [HttpGet("anomalias")]
    [Authorize(Roles = "Administrador,AnalistaDeData")]
    public async Task<IActionResult> GetAnomalias([FromQuery] int dias = 30)
    {
        var desde = DateTime.UtcNow.AddDays(-dias);
        var anomalias = await _db.PrediccionesIA
            .Where(p => p.EsAnomalia && p.FechaPrediccion >= desde)
            .Include(p => p.Cuenta).ThenInclude(c => c.Cliente)
            .OrderByDescending(p => p.ScoreAnomalia)
            .Take(100)
            .AsNoTracking()
            .ToListAsync();

        return Ok(new {
            Total    = anomalias.Count,
            Periodo  = $"Ultimos {dias} dias",
            Anomalias = anomalias.Select(a => new {
                a.CuentaId,
                NombreCliente  = a.Cuenta?.Cliente?.Nombre,
                a.ScoreAnomalia,
                a.ScorePago,
                a.FechaPrediccion
            })
        });
    }

    // GET api/reportes/pagos-recientes
    [HttpGet("pagos-recientes")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor")]
    public async Task<IActionResult> GetPagosRecientes([FromQuery] int dias = 7)
    {
        var desde = DateTime.UtcNow.AddDays(-dias);
        var pagos = await _db.Pagos
            .Where(p => p.FechaRegistro >= desde && p.Estado == EstadoPago.Aplicado)
            .Include(p => p.Cuenta).ThenInclude(c => c.Cliente)
            .OrderByDescending(p => p.FechaRegistro)
            .Take(100)
            .AsNoTracking()
            .ToListAsync();

        return Ok(new {
            TotalPagos   = pagos.Count,
            MontoTotal   = pagos.Sum(p => p.Monto),
            Periodo      = $"Ultimos {dias} dias",
            Pagos        = pagos.Select(p => new {
                p.Id, p.Monto,
                NombreCliente = p.Cuenta?.Cliente?.Nombre,
                Bucket        = p.Cuenta?.Bucket.ToString(),
                Canal         = p.Canal.ToString(),
                p.FechaPago
            })
        });
    }

    // GET api/reportes/promesas-vencidas
    [HttpGet("promesas-vencidas")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor")]
    public async Task<IActionResult> GetPromesasVencidas()
    {
        var promesas = await _db.Promesas
            .Where(p => p.Estado == EstadoPromesa.Pendiente && p.FechaPromesa < DateTime.UtcNow)
            .Include(p => p.Cuenta).ThenInclude(c => c.Cliente)
            .OrderBy(p => p.FechaPromesa)
            .AsNoTracking()
            .ToListAsync();

        // Actualizar a incumplidas
        foreach (var p in promesas) p.Estado = EstadoPromesa.Incumplida;
        await _db.SaveChangesAsync();

        return Ok(new {
            Total          = promesas.Count,
            MontoTotal     = promesas.Sum(p => p.MontoPrometido),
            Promesas       = promesas.Select(p => new {
                p.Id, p.MontoPrometido, p.FechaPromesa,
                NombreCliente = p.Cuenta?.Cliente?.Nombre,
                Bucket        = p.Cuenta?.Bucket.ToString()
            })
        });
    }
}
