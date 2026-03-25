# 360Collect API — DevNef
## Sistema Inteligente de Cobranza con IA

---

## Requisitos Previos

| Herramienta | Version minima |
|---|---|
| .NET SDK | 8.0 |
| Visual Studio | 2022 (17.8+) |
| PostgreSQL | 16 |
| Git | Cualquier version reciente |

---

## Inicio Rapido (5 pasos)

### 1. Clonar / Abrir el proyecto
Abre `360Collect.csproj` directamente en Visual Studio 2022.

### 2. Configurar la base de datos
Edita `appsettings.json` y ajusta tu cadena de conexion:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=360collect_db;Username=postgres;Password=Pollito01"
}
```

### 3. Instalar dependencias y aplicar migraciones
```bash
# Restaurar paquetes NuGet
dotnet restore

# Crear la migracion inicial
dotnet ef migrations add InitialCreate --output-dir Migrations

# Aplicar migracion (crea la BD automaticamente)
dotnet ef database update
```

> **Nota:** El seeder corre automaticamente al levantar la API si `Seeder.RunOnStartup = true`.

### 4. Ejecutar la API
```bash
dotnet run
# O presiona F5 en Visual Studio 2022
```

### 5. Probar en Swagger
Abre tu navegador en: **http://localhost:5000**

---

## Autenticacion en Swagger

1. Haz POST a `/api/usuarios/login` con:
```json
{
  "email": "admin@devnef.com",
  "password": "DevNef2026!"
}
```
2. Copia el token `Bearer {token}` que recibes.
3. Haz clic en el boton **Authorize** (candado) en Swagger.
4. Pega el token y haz clic en **Authorize**.

---

## Usuarios del Seeder

Todos los usuarios tienen la misma contrasena: `DevNef2026!`

| Email | Rol |
|---|---|
| admin@devnef.com | Administrador |
| ana.rodriguez@devnef.com | Administrador |
| luis.flores@devnef.com | Gestor de Cobranza |
| maria.torres@devnef.com | Gestor de Cobranza |
| sandra.lopez@devnef.com | Analista de Data |
| patricia.vega@devnef.com | Supervisor |
| david.herrera@devnef.com | Agente |
| oscar.medina@devnef.com | Soporte Tecnico |
| *(y 12 mas...)* | *varios roles* |

---

## Endpoints Principales

### Autenticacion
- `POST /api/usuarios/login` — Obtener token JWT
- `GET  /api/usuarios/me`    — Ver usuario autenticado

### Clientes
- `GET    /api/clientes`     — Listar (paginado + filtros)
- `GET    /api/clientes/{id}` — Detalle
- `POST   /api/clientes`     — Crear
- `PUT    /api/clientes/{id}` — Actualizar
- `DELETE /api/clientes/{id}` — Soft delete
- `GET    /api/clientes/{id}/cuentas` — Cuentas del cliente

### Cuentas
- `GET    /api/cuentas`                       — Listar (filtros: bucket, estado, agente)
- `GET    /api/cuentas/{id}`                  — Detalle
- `POST   /api/cuentas`                       — Crear (asigna bucket automaticamente)
- `POST   /api/cuentas/{id}/pago`             — Registrar pago
- `POST   /api/cuentas/{id}/promesa`          — Registrar promesa
- `POST   /api/cuentas/{id}/recalcular-bucket` — Actualizar bucket segun dias mora
- `GET    /api/cuentas/{id}/prediccion`       — Prediccion IA
- `GET    /api/cuentas/{id}/historial-bucket` — Historial de movimientos
- `GET    /api/cuentas/bucket/{bucket}`       — Cuentas por bucket

### Campanas
- `GET    /api/campanas`               — Listar campanas
- `POST   /api/campanas`               — Crear campana
- `PUT    /api/campanas/{id}/estado`   — Cambiar estado
- `POST   /api/campanas/{id}/ejecutar` — Ejecutar envio masivo
- `GET    /api/campanas/{id}/estadisticas` — Metricas de la campana

### Reportes / KPIs
- `GET /api/reportes/kpis`                 — KPIs globales del sistema
- `GET /api/reportes/distribucion-buckets` — Distribucion de cartera
- `GET /api/reportes/productividad-agentes` — Rendimiento por agente
- `GET /api/reportes/anomalias`            — Cuentas anomalas detectadas por IA
- `GET /api/reportes/pagos-recientes`      — Pagos de los ultimos N dias
- `GET /api/reportes/promesas-vencidas`    — Promesas incumplidas

### Usuarios
- `GET    /api/usuarios`               — Listar usuarios (Admin/Supervisor)
- `POST   /api/usuarios`               — Crear usuario (Admin)
- `PUT    /api/usuarios/{id}/rol`      — Cambiar rol (Admin)
- `PUT    /api/usuarios/{id}/password` — Cambiar contrasena
- `DELETE /api/usuarios/{id}`          — Desactivar (Admin)
- `GET    /api/usuarios/roles`         — Listar roles disponibles

---

## Logica de Buckets

| Bucket | Dias de Mora |
|---|---|
| PREVENT  | 0 dias (pago vencido, sin corte) |
| BK1      | 1 – 30 dias |
| BK2      | 31 – 60 dias |
| BK3      | 61 – 90 dias |
| BK4      | 91 – 120 dias |
| BK5      | 121 – 180 dias |
| RECOVERY | +180 dias |

---

## Estructura del Proyecto

```
360Collect/
├── Controllers/
│   ├── ClientesController.cs
│   ├── CuentasController.cs
│   ├── CampanasReportesControllers.cs
│   └── UsuariosController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── DatabaseSeeder.cs
├── DTOs/
│   └── Dtos.cs
├── Migrations/          ← generadas por EF Core
├── Models/
│   ├── Entities.cs
│   └── Enums.cs
├── Services/
│   └── Services.cs      ← JWT, IA simulada, Comunicaciones stub
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
└── 360Collect.csproj
```

---

## Extender con IA Real (XGBoost)

Reemplaza `IAServiceSimulado` en `Services/Services.cs` con llamadas HTTP al microservicio Python:

```python
# microservicio_ia/main.py (FastAPI + XGBoost)
from fastapi import FastAPI
import joblib, numpy as np
app = FastAPI()
model = joblib.load("xgboost_model.pkl")

@app.post("/predecir")
def predecir(cuenta: dict):
    features = np.array([[cuenta["dias_mora"], cuenta["monto"], ...]])
    score = float(model.predict_proba(features)[0][1] * 100)
    return {"score_pago": score, "bucket_predicho": calcular_bucket(cuenta["dias_mora"])}
```

---

## Extender con Twilio (WhatsApp / SMS / Llamadas)

En `Services/Services.cs`, reemplaza los stubs en `ComunicacionServiceStub`:

```csharp
// NuGet: Twilio
using Twilio;
using Twilio.Rest.Api.V2010.Account;

TwilioClient.Init(accountSid, authToken);
MessageResource.Create(
    from: new PhoneNumber("whatsapp:+14155238886"),
    to:   new PhoneNumber($"whatsapp:{numero}"),
    body: mensaje
);
```

---

## Health Check
```
GET /health
```

---

## Tecnologias
- ASP.NET Core Web API (.NET 8)
- Entity Framework Core 8 + Npgsql (PostgreSQL 16)
- JWT Bearer Authentication
- BCrypt.Net-Next (hashing de contrasenas)
- Bogus (datos ficticios en el seeder)
- Swashbuckle / Swagger UI
