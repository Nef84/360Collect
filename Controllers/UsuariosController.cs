// ============================================================
//  Controllers/UsuariosController.cs  –  Auth + CRUD usuarios
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
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(AppDbContext db, IJwtService jwt, ILogger<UsuariosController> logger)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────
    // POST api/usuarios/login
    // ──────────────────────────────────────────────────────────
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(req.Password, usuario.PasswordHash))
        {
            _logger.LogWarning("Login fallido para: {email}", req.Email);
            return Unauthorized(new { mensaje = "Credenciales invalidas." });
        }

        usuario.UltimoAcceso = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerarToken(usuario);
        var jwtSettings = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>().GetSection("JwtSettings");
        var expiracion = DateTime.UtcNow.AddHours(double.Parse(jwtSettings["ExpirationHours"]!));

        _logger.LogInformation("Login exitoso: {email} | Rol: {rol}", usuario.Email, usuario.Rol);

        return Ok(new LoginResponse(
            token, usuario.Nombre, usuario.Email,
            usuario.Rol.ToString(), expiracion
        ));
    }

    // GET api/usuarios
    [HttpGet]
    [Authorize(Roles = "Administrador,Supervisor")]
    [ProducesResponseType(typeof(IEnumerable<UsuarioDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool? activo = null)
    {
        var q = _db.Usuarios.AsNoTracking().AsQueryable();
        if (activo.HasValue) q = q.Where(u => u.Activo == activo.Value);

        var lista = await q.OrderBy(u => u.Nombre)
            .Select(u => ToDto(u))
            .ToListAsync();

        return Ok(lista);
    }

    // GET api/usuarios/{id}
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound(new { mensaje = "Usuario no encontrado." });
        return Ok(ToDto(u));
    }

    // GET api/usuarios/me
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        return Ok(new
        {
            id    = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            nombre = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            rol   = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }

    // POST api/usuarios
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(UsuarioDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { mensaje = "Ya existe un usuario con ese email." });

        var usuario = new Usuario
        {
            Nombre       = req.Nombre,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Rol          = req.Rol,
            Activo       = true,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Usuario creado: {email} | Rol: {rol}", usuario.Email, usuario.Rol);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, ToDto(usuario));
    }

    // PUT api/usuarios/{id}/rol
    [HttpPut("{id:int}/rol")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> CambiarRol(int id, [FromBody] RolUsuario nuevoRol)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound(new { mensaje = "Usuario no encontrado." });
        usuario.Rol = nuevoRol;
        await _db.SaveChangesAsync();
        return Ok(ToDto(usuario));
    }

    // PUT api/usuarios/{id}/password
    [HttpPut("{id:int}/password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword(int id, [FromBody] CambiarPasswordRequest req)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound(new { mensaje = "Usuario no encontrado." });

        if (!BCrypt.Net.BCrypt.Verify(req.PasswordActual, usuario.PasswordHash))
            return BadRequest(new { mensaje = "La contrasena actual es incorrecta." });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NuevoPassword);
        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Contrasena actualizada correctamente." });
    }

    // DELETE api/usuarios/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound(new { mensaje = "Usuario no encontrado." });
        usuario.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET api/usuarios/roles
    [HttpGet("roles")]
    [AllowAnonymous]
    public IActionResult GetRoles()
    {
        var roles = Enum.GetValues<RolUsuario>()
            .Select(r => new { id = (int)r, nombre = r.ToString() });
        return Ok(roles);
    }

    private static UsuarioDto ToDto(Usuario u) => new(
        u.Id, u.Nombre, u.Email, u.Rol.ToString(),
        u.Activo, u.FechaCreacion, u.UltimoAcceso
    );
}
