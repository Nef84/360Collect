// ============================================================
//  Controllers/ClientesController.cs
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _360Collect.Data;
using _360Collect.DTOs;
using _360Collect.Models;

namespace _360Collect.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(AppDbContext db, ILogger<ClientesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET api/clientes?busqueda=&pagina=1&tamanoPagina=20
    [HttpGet]
    [Authorize(Roles = "Administrador,GestorDeCobranza,Supervisor,AnalistaDeData")]
    [ProducesResponseType(typeof(PaginatedResult<ClienteDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] FiltroClienteRequest filtro)
    {
        var query = _db.Clientes
            .Include(c => c.Cuentas)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var b = filtro.Busqueda.ToLower();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(b) ||
                (c.Documento != null && c.Documento.Contains(b)) ||
                (c.Email != null && c.Email.ToLower().Contains(b)));
        }

        if (filtro.Activo.HasValue)
            query = query.Where(c => c.Activo == filtro.Activo.Value);

        int total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nombre)
            .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Ok(new PaginatedResult<ClienteDto>(
            items, total, filtro.Pagina, filtro.TamanoPagina,
            (int)Math.Ceiling((double)total / filtro.TamanoPagina)
        ));
    }

    // GET api/clientes/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClienteDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Clientes.Include(x => x.Cuentas)
                    .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { mensaje = "Cliente no encontrado." });
        return Ok(ToDto(c));
    }

    // POST api/clientes
    [HttpPost]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    [ProducesResponseType(typeof(ClienteDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Crear([FromBody] CrearClienteRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (req.Email is not null && await _db.Clientes.AnyAsync(c => c.Email == req.Email))
            return BadRequest(new { mensaje = "Ya existe un cliente con ese email." });

        var cliente = new Cliente
        {
            Nombre          = req.Nombre,
            Documento       = req.Documento,
            Telefono        = req.Telefono,
            Email           = req.Email,
            WhatsApp        = req.WhatsApp,
            CanalPreferido  = req.CanalPreferido,
            Direccion       = req.Direccion,
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Cliente creado: {id} – {nombre}", cliente.Id, cliente.Nombre);
        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, ToDto(cliente));
    }

    // PUT api/clientes/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador,GestorDeCobranza")]
    [ProducesResponseType(typeof(ClienteDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarClienteRequest req)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente is null) return NotFound(new { mensaje = "Cliente no encontrado." });

        if (req.Nombre       is not null) cliente.Nombre          = req.Nombre;
        if (req.Telefono     is not null) cliente.Telefono         = req.Telefono;
        if (req.Email        is not null) cliente.Email            = req.Email;
        if (req.WhatsApp     is not null) cliente.WhatsApp         = req.WhatsApp;
        if (req.CanalPreferido.HasValue)  cliente.CanalPreferido   = req.CanalPreferido.Value;
        if (req.Direccion    is not null) cliente.Direccion        = req.Direccion;

        await _db.SaveChangesAsync();
        return Ok(ToDto(cliente));
    }

    // DELETE api/clientes/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente is null) return NotFound(new { mensaje = "Cliente no encontrado." });
        cliente.Activo = false; // Soft delete
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET api/clientes/{id}/cuentas
    [HttpGet("{id:int}/cuentas")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetCuentas(int id)
    {
        var existe = await _db.Clientes.AnyAsync(c => c.Id == id);
        if (!existe) return NotFound(new { mensaje = "Cliente no encontrado." });

        var cuentas = await _db.Cuentas
            .Where(c => c.ClienteId == id)
            .Include(c => c.Agente)
            .AsNoTracking()
            .ToListAsync();

        return Ok(cuentas.Select(c => new CuentaDto(
            c.Id, c.ClienteId, "",
            c.Monto, c.SaldoPendiente,
            c.FechaVencimiento, c.DiasMora,
            c.Bucket.ToString(), c.Estado.ToString(),
            c.ScoreIA, c.NumeroCuenta, c.Producto,
            c.Agente?.Nombre, c.FechaCreacion
        )));
    }

    private static ClienteDto ToDto(Cliente c) => new(
        c.Id, c.Nombre, c.Documento, c.Telefono,
        c.Email, c.WhatsApp, c.CanalPreferido.ToString(),
        c.ScoreRiesgo, c.Activo, c.FechaRegistro,
        c.Cuentas?.Count ?? 0,
        c.Cuentas?.Sum(ct => ct.SaldoPendiente) ?? 0
    );
}
