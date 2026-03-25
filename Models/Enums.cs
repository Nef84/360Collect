// ============================================================
//  Models/Enums.cs  –  Enumeraciones del dominio 360Collect
// ============================================================
namespace _360Collect.Models;

public enum Bucket
{
    PREVENT   = 0,
    BK1       = 1,
    BK2       = 2,
    BK3       = 3,
    BK4       = 4,
    BK5       = 5,
    RECOVERY  = 6
}

public enum RolUsuario
{
    Administrador       = 1,
    GestorDeCobranza    = 2,
    AnalistaDeData      = 3,
    Supervisor          = 4,
    Agente              = 5,
    SoporteTecnico      = 6
}

public enum CanalComunicacion
{
    WhatsApp  = 1,
    SMS       = 2,
    Email     = 3,
    Llamada   = 4
}

public enum EstadoCuenta
{
    Activa      = 1,
    EnGestion   = 2,
    Recuperada  = 3,
    Castigada   = 4,
    EnAgencia   = 5
}

public enum EstadoPago
{
    Pendiente   = 1,
    Aplicado    = 2,
    Revertido   = 3,
    EnDisputa   = 4
}

public enum EstadoPromesa
{
    Pendiente   = 1,
    Cumplida    = 2,
    Incumplida  = 3,
    Cancelada   = 4
}

public enum EstadoCampana
{
    Borrador    = 1,
    Activa      = 2,
    Pausada     = 3,
    Finalizada  = 4
}

public enum ResultadoInteraccion
{
    Contactado          = 1,
    NoContesta          = 2,
    NumeroInvalido      = 3,
    PromesaDePago       = 4,
    PagoParcial         = 5,
    PagoTotal           = 6,
    SeNiega             = 7,
    BuzonDeVoz          = 8
}
