# LavAR

**La lavandería deja de anotar en un cuaderno.** El mostrador carga el pedido, el cliente lo sigue
con un código de 6 letras y la lavandería avisa por WhatsApp sin tipear nada.

[![CI](https://github.com/lucabaello1998/lavar/actions/workflows/ci.yml/badge.svg)](https://github.com/lucabaello1998/lavar/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![React](https://img.shields.io/badge/React-19-087ea4)
![SQLite](https://img.shields.io/badge/SQLite-local-003b57)
![Tests](https://img.shields.io/badge/tests-68-2ea44f)
![Licencia](https://img.shields.io/badge/licencia-MIT-blue)

---

## El problema

Una lavandería chica trabaja con un cuaderno, una birome y el WhatsApp del dueño. El cliente deja
tres kilos de ropa y un acolchado, y a las dos horas empieza el "¿ya está?". Cada pregunta corta la
tarea de alguien, y las que se pierden son las que terminan en un reclamo.

Lo que **no** funciona para ese negocio: una app para el cliente. Nadie se descarga una app para
saber si su campera está lavada.

Lo que sí:

- El cliente recibe un **código** ("K7M2QP") y una URL. Abre el link, ve el estado, listo. Sin
  cuenta, sin contraseña, sin instalar nada.
- La lavandería administra desde el **panel web** (sirve igual en la compu del local, la tablet o el
  celular): qué dejó cada cliente, en qué estado está, los precios y quiénes son sus clientes.
- El aviso sale por **WhatsApp** con el mensaje ya escrito: un clic, sin APIs pagas.

## Cómo se usa

### La lavandería

1. **Nuevo pedido**: nombre y teléfono del cliente, qué dejó (kilos, acolchados, zapatillas,
   tintorería) y para cuándo. El total se calcula solo.
2. En la lista de pedidos, cada tarjeta tiene un botón para avanzar el estado:
   *Recibido → En proceso → Listo → Entregado*.
3. Cuando está listo, **Avisar** abre WhatsApp con el mensaje escrito ("Hola Ana, tu pedido K7M2QP
   está listo para retirar. Lo tenés listo el 12/11. — Lavadero Rivadavia").
4. **Servicios** es la lista de precios: cambiarlos no toca los pedidos ya cargados.
5. **Clientes** muestra quién tiene ropa en curso, con búsqueda por nombre o teléfono.

### El cliente

Abre `https://tu-lavanderia/p/K7M2QP` (el link que le mandó la lavandería) y ve:

- el estado en una línea de tiempo,
- cuándo está prometido,
- qué dejó y cuánto sale,
- un campo para consultar otro código si tiene más de un pedido.

No se registra, no instala nada, no puede ver los pedidos de otro: el código es la llave.

## Correrlo local

Necesitás **.NET 8** y **Node 20+**.

```bash
# terminal 1: la API (http://127.0.0.1:8123)
cd src/Lavar.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://127.0.0.1:8123
```

En Windows (PowerShell):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:8123"
dotnet run --project src/Lavar.Api --no-launch-profile
```

```bash
# terminal 2: el panel con hot reload (http://localhost:5173)
cd web
npm install
npm run dev
```

El proxy de Vite manda `/api` a la API, así que no hay CORS de por medio. Entrá con el token de
desarrollo: **`dev-token`** (está en `src/Lavar.Api/appsettings.Development.json`).

En desarrollo la API arranca con **datos de demo** (dos pedidos) para no mirar una pantalla vacía.

### Todo junto, como en producción

El panel compilado se sirve desde la misma app que la API:

```bash
cd web && npm run build
```

```powershell
# PowerShell
Remove-Item src\Lavar.Api\wwwroot -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item web\dist src\Lavar.Api\wwwroot -Recurse
```

```bash
# bash
rm -rf src/Lavar.Api/wwwroot && cp -r web/dist src/Lavar.Api/wwwroot
```

Después, `dotnet run --project src/Lavar.Api` y abrí <http://127.0.0.1:5000>: el panel y la API
salen del mismo puerto. Es lo que hace la imagen Docker.

## Configuración

| Variable | Para qué | Default |
|---|---|---|
| `LAVAR_ADMIN_TOKEN` | Token del panel (**obligatorio** en producción) | `dev-token` en Development |
| `ConnectionStrings__Lavar` | Cadena de SQLite | `Data Source=lavar.db` |
| `Lavanderia__Nombre` | El nombre que aparece en el panel y en los avisos | `Lavandería de barrio` |
| `Lavanderia__Demo` | Carga dos pedidos de ejemplo si la base está vacía | `false` (`true` en Development) |
| `Cors__Origenes__0` … | Orígenes permitidos si el panel se sirve aparte | `localhost:5173` |

El token es lo único que separa el panel del mundo: es una contraseña compartida, no hay usuarios ni
roles (una lavandería tiene un dueño y, con suerte, un empleado). La API lo compara en tiempo
constante y responde `401` sin él.

## La API

Todo lo que usa el panel es HTTP normal; sirve para una app móvil o una integración.

| Método | Ruta | Quién | Qué hace |
|---|---|---|---|
| `GET` | `/api/pedidos/{codigo}` | público | El estado del pedido. Acepta el código en minúsculas o con guiones. Limitado a 60 consultas por minuto por IP (`RateLimit:PublicoPorMinuto`) para que nadie pruebe códigos al azar |
| `GET` | `/api/servicios` | público | La lista de precios vigente |
| `GET` | `/api/salud` | público | Health check |
| `GET` | `/api/admin/resumen` | panel | Pedidos y facturado del día, clientes, por estado |
| `GET` | `/api/admin/pedidos?estado=&q=` | panel | Lista con filtros por estado, código, cliente o teléfono |
| `POST` | `/api/admin/pedidos` | panel | Carga un pedido y devuelve su código |
| `POST` | `/api/admin/pedidos/{id}/estado` | panel | Cambia el estado (valida la transición) |
| `GET` | `/api/admin/pedidos/{id}/aviso` | panel | Mensaje listo + link de WhatsApp |
| `GET` | `/api/admin/clientes` · `POST` · `PATCH` | panel | Clientes |
| `GET` | `/api/admin/servicios` · `POST` · `PATCH` | panel | Servicios y precios |

Ejemplo:

```bash
curl -X POST http://127.0.0.1:8123/api/admin/pedidos \
  -H "X-Admin-Token: dev-token" -H "Content-Type: application/json" \
  -d '{"clienteNombre":"Ana Gómez","clienteTelefono":"11 5555 1234",
       "prometidoPara":"2026-11-12",
       "items":[{"servicioId":"<id del servicio>","cantidad":3.5}]}'
```

En desarrollo hay Swagger en <http://127.0.0.1:8123/swagger>.

## Las reglas (por qué un pedido no se rompe)

Están en el dominio (`src/Lavar.Core/Modelos/Pedido.cs`), no en el panel, así que valen para
cualquier cliente de la API:

- **El código** tiene 6 caracteres de un alfabeto sin `0/O` ni `1/I/L`: se dicta por teléfono y se
  copia de un papel. Se generan con `RandomNumberGenerator`, no con `Random`.
- **El precio se congela** al crear el pedido: si mañana sube el kilo, el pedido de ayer sigue
  saliendo lo que se acordó.
- **A Entregado solo se llega desde Listo.** Entregar algo que todavía no está listo es el error que
  deja al cliente sin ropa; la API responde `400` con el motivo.
- **Entregado y Cancelado son finales**: no se deshacen. Entre los demás se puede corregir para atrás
  (si tocaron "Listo" sin querer, vuelve a En proceso).
- **Los mismos 6 dígitos de la vida real**: un acolchado no se cobra como "1,5 unidades" (la API lo
  rechaza), pero los kilos sí pueden ser decimales.

## Los tests

```bash
dotnet test
```

```
Correctas! - Con error: 0, Superado: 68, Omitido: 0, Total: 68
```

Cubren tres capas:

- **Reglas**: transiciones de estado válidas e inválidas, precio congelado, totales, cantidades
  redondeadas, códigos sin caracteres ambiguos y sin repetirse.
- **Avisos**: normalización de teléfonos argentinos (`011 15-5555-1234` → `5491155551234`), link de
  WhatsApp codificado, y qué dice el mensaje en cada estado.
- **API real**: levantan la app con `WebApplicationFactory` y le pegan por HTTP — token faltante o
  inválido (`401`), alta de pedido, consulta pública en minúsculas, `404` de un código inexistente,
  `400` al entregar algo que no está listo, buscador, resumen.

El CI corre además el build con `-warnaserror`, el build del panel y un smoke test de la imagen
Docker (arranca, sirve el panel, sin token da 401, con token responde).

## Deploy

### Docker (una sola imagen)

```bash
docker build -t lavar .
docker run -d -p 8080:8080 \
  -e LAVAR_ADMIN_TOKEN="un-token-largo-y-random" \
  -e Lavanderia__Nombre="Lavadero Rivadavia" \
  -v lavar-datos:/datos \
  lavar
# → http://localhost:8080
```

O con Compose: `docker compose up -d --build` (editá el token en `docker-compose.yml` antes).

- La base es **SQLite** en `/datos/lavar.db`. Para una lavandería alcanza y sobra: un pedido por
  cliente cada tanto, no miles por minuto. **El backup es copiar esa carpeta** (o el volumen).
- El contenedor sirve la API y el panel en el mismo puerto: no hay que configurar un front aparte ni
  CORS.
- Health check: `GET /api/salud`.
- Detrás de un proxy (Caddy, Nginx, el de Fly/Railway) no hace falta nada especial: `force_https`
  del proxy alcanza.

### Fly.io / Railway

`fly.toml` no está incluido a propósito: cambia el nombre de la app y la región. Lo mínimo:

```bash
fly launch --no-deploy --dockerfile Dockerfile
fly secrets set LAVAR_ADMIN_TOKEN="$(openssl rand -hex 24)"
fly volumes create datos --size 1
# agregar [mounts] source="datos" destination="/datos" en fly.toml y:
fly deploy
```

En Railway: *New Project → Deploy from repo*, la imagen se detecta sola, y una variable
`LAVAR_ADMIN_TOKEN` + un volumen en `/datos`.

## El próximo paso

Lo que sigue está pensado sobre la misma API, sin reescribir nada:

- **App Flutter para el cliente** (opcional, para quien quiera notificaciones push en vez de abrir el
  link). El endpoint público ya devuelve todo lo que la app necesita.
- **WhatsApp**: hoy el aviso es un link que abre el chat con el texto escrito. Con la Cloud API de
  Meta (o SMS con Twilio) se manda solo cuando el pedido pasa a Listo; es el primer gasto real del
  proyecto y el que más tiempo ahorra.
- **Cobros**: registrar seña o pago del pedido y cerrar la caja del día.
- **Monetización** (para pensarla, no decidida): una suscripción mensual fija es lo más sano para el
  negocio (no se cobra por pedido, así no se castiga al que más trabaja); alternativa, gratis hasta
  N pedidos por mes y un pago único por sucursal. Un código QR en el mostrador que apunte a la vista
  pública es la mejor pieza de venta: el cliente de al lado ya lo está viendo.

## Límites

- Es de **una sucursal**. Multisucursal es agregar `SucursalId` a pedidos, servicios y usuarios del
  panel, no un rediseño.
- No hay usuarios ni roles en el panel: un token compartido. Para un local es lo correcto; para una
  cadena, no.
- Sin notificaciones automáticas: el aviso lo manda una persona (a propósito, para no gastar en APIs
  antes de tener clientes reales).
- Los montos son en pesos y sin IVA discriminado: es un ticket de lavandería, no una factura.

## Licencia

MIT. Ver [LICENSE](LICENSE).
