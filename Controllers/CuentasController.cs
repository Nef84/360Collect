// ============================================================
//  Controllers/CuentasController.cs
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
public class CuentasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IIAService _ia;
    private readonly ILogger<CuentasController> _logger;

    public CuentasController(AppDbContext db, IIAService ia, ILogger<CuentasController> logger)
    {
        _db = db;
        _ia = ia;
        _logger = logger;
    }

    // GET api/cuentas
    [HttpGet]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor,AnalistaDeData")]
    [ProducesResponseType(typeof(PaginatedResult<CuentaDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] FiltroCuentaRequest filtro)
    {
        var q = _db.Cuentas
            .Include(c => c.Cliente)
            .Include(c => c.Agente)
            .AsNoTracking()
            .AsQueryable();

        if (filtro.Bucket.HasValue)    q = q.Where(c => c.Bucket    == filtro.Bucket.Value);
        if (filtro.Estado.HasValue)    q = q.Where(c => c.Estado    == filtro.Estado.Value);
        if (filtro.AgenteId.HasValue)  q = q.Where(c => c.AgenteId  == filtro.AgenteId.Value);
        if (filtro.ClienteId.HasValue) q = q.Where(c => c.ClienteId == filtro.ClienteId.Value);
        if (filtro.MontoMin.HasValue)  q = q.Where(c => c.Monto     >= filtro.MontoMin.Value);
        if (filtro.MontoMax.HasValue)  q = q.Where(c => c.Monto     <= filtro.MontoMax.Value);

        int total = await q.CountAsync();
        var items = await q
            .OrderByDescending(c => c.DiasMora)
            .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Ok(new PaginatedResult<CuentaDto>(
            items, total, filtro.Pagina, filtro.TamanoPagina,
            (int)Math.Ceiling((double)total / filtro.TamanoPagina)
        ));
    }

    // GET api/cuentas/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CuentaDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Cuentas
            .Include(x => x.Cliente)
            .Include(x => x.Agente)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { mensaje = "Cuenta no encontrada." });
        return Ok(ToDto(c));
    }

    // GET api/cuentas/bucket/{bucket}
    [HttpGet("bucket/{bucket}")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor,AnalistaDeData")]
    public async Task<IActionResult> GetByBucket(Bucket bucket, [FromQuery] int pagina = 1, [FromQuery] int tam = 20)
    {
        var total = await _db.Cuentas.CountAsync(c => c.Bucket == bucket);
        var items = await _db.Cuentas
            .Where(c => c.Bucket == bucket)
            .Include(c => c.Cliente)
            .Include(c => c.Agente)
            .AsNoTracking()
            .OrderByDescending(c => c.SaldoPendiente)
            .Skip((pagina - 1) * tam).Take(tam)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Ok(new PaginatedResult<CuentaDto>(
            items, total, pagina, tam, (int)Math.Ceiling((double)total / tam)
        ));
    }

    // POST api/cuentas
    [HttpPost]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    [ProducesResponseType(typeof(CuentaDto), 201)]
    public async Task<IActionResult> Crear([FromBody] CrearCuentaRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _db.Clientes.AnyAsync(c => c.Id == req.ClienteId))
            return BadRequest(new { mensaje = "Cliente no existe." });

        int diasMora = BucketService.CalcularDiasMora(req.FechaVencimiento);
        var bucket   = BucketService.AsignarBucket(diasMora);

        var cuenta = new Cuenta
        {
            ClienteId        = req.ClienteId,
            Monto            = req.Monto,
            SaldoPendiente   = req.Monto,
            FechaVencimiento = req.FechaVencimiento,
            DiasMora         = diasMora,
            Bucket           = bucket,
            NumeroCuenta     = req.NumeroCuenta ?? $"DC-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            Producto         = req.Producto,
            AgenteId         = req.AgenteId,
        };

        _db.Cuentas.Add(cuenta);
        await _db.SaveChangesAsync();

        // Registrar en historial
        _db.BucketHistorial.Add(new BucketHistorial
        {
            CuentaId = cuenta.Id, BucketAnterior = bucket, BucketNuevo = bucket,
            Motivo = "Cuenta creada"
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cuenta creada: {id}, Bucket: {b}", cuenta.Id, bucket);
        return CreatedAtAction(nameof(GetById), new { id = cuenta.Id },
            ToDto(await _db.Cuentas.Include(c => c.Cliente).Include(c => c.Agente).FirstAsync(c => c.Id == cuenta.Id)));
    }

    // PUT api/cuentas/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    [ProducesResponseType(typeof(CuentaDto), 200)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarCuentaRequest req)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });

        if (req.Estado.HasValue)         cuenta.Estado         = req.Estado.Value;
        if (req.AgenteId.HasValue)       cuenta.AgenteId       = req.AgenteId.Value;
        if (req.SaldoPendiente.HasValue) cuenta.SaldoPendiente = req.SaldoPendiente.Value;

        cuenta.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(await GetCuentaDtoAsync(id));
    }

    // POST api/cuentas/{id}/recalcular-bucket
    [HttpPost("{id:int}/recalcular-bucket")]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    public async Task<IActionResult> RecalcularBucket(int id)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });

        var bucketAnterior = cuenta.Bucket;
        cuenta.DiasMora    = BucketService.CalcularDiasMora(cuenta.FechaVencimiento);
        var bucketNuevo    = BucketService.AsignarBucket(cuenta.DiasMora);

        if (bucketNuevo != bucketAnterior)
        {
            cuenta.Bucket = bucketNuevo;
            _db.BucketHistorial.Add(new BucketHistorial
            {
                CuentaId = cuenta.Id, BucketAnterior = bucketAnterior, BucketNuevo = bucketNuevo,
                Motivo = "Recalculo manual de bucket"
            });
        }

        cuenta.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new {
            cuentaId = id,
            bucketAnterior = bucketAnterior.ToString(),
            bucketNuevo = bucketNuevo.ToString(),
            diasMora = cuenta.DiasMora,
            cambio = bucketNuevo != bucketAnterior
        });
    }

    // POST api/cuentas/{id}/pago
    [HttpPost("{id:int}/pago")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Agente")]
    public async Task<IActionResult> RegistrarPago(int id, [FromBody] RegistrarPagoRequest req)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });
        if (req.Monto <= 0) return BadRequest(new { mensaje = "El monto debe ser mayor a 0." });

        var pago = new Pago
        {
            CuentaId   = id,
            Monto      = req.Monto,
            FechaPago  = req.FechaPago,
            Canal      = req.Canal,
            Estado     = EstadoPago.Aplicado,
            Referencia = req.Referencia ?? $"REF-{Guid.NewGuid().ToString()[..8].ToUpper()}"
        };

        cuenta.SaldoPendiente = Math.Max(0, cuenta.SaldoPendiente - req.Monto);
        if (cuenta.SaldoPendiente == 0) cuenta.Estado = EstadoCuenta.Recuperada;

        _db.Pagos.Add(pago);
        await _db.SaveChangesAsync();

        return Ok(new {
            pagoId      = pago.Id,
            saldoAnterior = cuenta.SaldoPendiente + req.Monto,
            saldoActual = cuenta.SaldoPendiente,
            estado      = cuenta.Estado.ToString()
        });
    }

    // POST api/cuentas/{id}/promesa
    [HttpPost("{id:int}/promesa")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Agente")]
    public async Task<IActionResult> RegistrarPromesa(int id, [FromBody] RegistrarPromesaRequest req)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });

        var promesa = new Promesa
        {
            CuentaId       = id,
            MontoPrometido = req.MontoPrometido,
            FechaPromesa   = req.FechaPromesa,
            Estado         = EstadoPromesa.Pendiente,
            Notas          = req.Notas
        };

        _db.Promesas.Add(promesa);
        await _db.SaveChangesAsync();
        return Ok(promesa);
    }

    // GET api/cuentas/{id}/prediccion
    [HttpGet("{id:int}/prediccion")]
    [Authorize(Roles = "Administrador,AnalistaDeData,GestorDeCobranza,Supervisor")]
    public async Task<IActionResult> GetPrediccion(int id)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });

        var pred = _ia.GenerarPrediccion(cuenta);
        pred.CuentaId = id;
        _db.PrediccionesIA.Add(pred);
        await _db.SaveChangesAsync();
        return Ok(pred);
    }

    // GET api/cuentas/{id}/historial-bucket
    [HttpGet("{id:int}/historial-bucket")]
    public async Task<IActionResult> GetHistorialBucket(int id)
    {
        var historial = await _db.BucketHistorial
            .Where(h => h.CuentaId == id)
            .OrderByDescending(h => h.Fecha)
            .AsNoTracking()
            .ToListAsync();
        return Ok(historial);
    }

    // GET api/cuentas/{id}/interacciones
    [HttpGet("{id:int}/interacciones")]
    public async Task<IActionResult> GetInteracciones(int id)
    {
        var lista = await _db.Interacciones
            .Where(i => i.CuentaId == id)
            .Include(i => i.Usuario)
            .OrderByDescending(i => i.Fecha)
            .AsNoTracking()
            .ToListAsync();
        return Ok(lista);
    }

    // POST api/cuentas/{id}/interaccion
    [HttpPost("{id:int}/interaccion")]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Agente")]
    public async Task<IActionResult> RegistrarInteraccion(int id, [FromBody] RegistrarInteraccionRequest req)
    {
        var cuenta = await _db.Cuentas.FindAsync(id);
        if (cuenta is null) return NotFound(new { mensaje = "Cuenta no encontrada." });

        var usuarioIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        int? usuarioId = usuarioIdClaim is not null ? int.Parse(usuarioIdClaim.Value) : null;

        var inter = new Interaccion
        {
            CuentaId         = id,
            UsuarioId        = usuarioId,
            Canal            = req.Canal,
            Resultado        = req.Resultado,
            Notas            = req.Notas,
            DuracionSegundos = req.DuracionSegundos,
            Fecha            = DateTime.UtcNow
        };

        _db.Interacciones.Add(inter);
        await _db.SaveChangesAsync();
        return Ok(inter);
    }

    private async Task<CuentaDto> GetCuentaDtoAsync(int id)
    {
        var c = await _db.Cuentas.Include(x => x.Cliente).Include(x => x.Agente)
                    .AsNoTracking().FirstAsync(x => x.Id == id);
        return ToDto(c);
    }

    private static CuentaDto ToDto(Cuenta c) => new(
        c.Id, c.ClienteId, c.Cliente?.Nombre ?? "",
        c.Monto, c.SaldoPendiente, c.FechaVencimiento,
        c.DiasMora, c.Bucket.ToString(), c.Estado.ToString(),
        c.ScoreIA, c.NumeroCuenta, c.Producto,
        c.Agente?.Nombre, c.FechaCreacion
    );
}
