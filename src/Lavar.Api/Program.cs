using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Lavar.Api;
using Lavar.Api.Data;
using Lavar.Core;
using Lavar.Core.Modelos;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration.GetConnectionString("Lavar")
    ?? "Data Source=lavar.db";

builder.Services.AddDbContext<LavarDbContext>(opciones => opciones.UseSqlite(cadena));
builder.Services.ConfigureHttpJsonOptions(opciones =>
{
    // Los estados viajan como texto ("Listo"), no como números: el panel y
    // cualquier cliente de la API tienen que poder leerlos sin adivinar el orden
    // del enum.
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opciones.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

// El código es la única llave del pedido: sin límite, alguien podría probar
// códigos al azar (o con un script) hasta pegarle al de otro cliente. El panel no
// pasa por acá, así que un límite bajo no molesta a la lavandería.
var limitePublico = builder.Configuration.GetValue("RateLimit:PublicoPorMinuto", 60);
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddFixedWindowLimiter("publico", ventana =>
    {
        ventana.PermitLimit = Math.Max(1, limitePublico);
        ventana.Window = TimeSpan.FromMinutes(1);
        ventana.QueueLimit = 0;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- arranque: base lista y servicios cargados ---------------------------------
using (var alcance = app.Services.CreateScope())
{
    var db = alcance.ServiceProvider.GetRequiredService<LavarDbContext>();
    await db.Database.EnsureCreatedAsync();
    await Semilla.ServiciosAsync(db);
    // "1", "true" y "si" valen lo mismo: es una variable de entorno que se
    // escribe a mano en docker-compose o en el panel del hosting.
    if (Verdadero(app.Configuration["Lavanderia:Demo"]))
        await Semilla.DemoAsync(db, NombreLavanderia(app.Configuration));
}

// --- errores ---------------------------------------------------------------
app.Use(async (contexto, siguiente) =>
{
    try
    {
        await siguiente(contexto);
    }
    catch (ReglaDeNegocioException ex)
    {
        // Una regla del negocio no es un 500: es un "así no se puede".
        contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
        await contexto.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();

// Cabeceras de seguridad: el panel sólo carga sus propios assets, así que se
// puede cerrar el framing, el sniffing y los permisos que no usa.
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    await siguiente(contexto);
});

// El panel se sirve desde el mismo contenedor que la API: si hay wwwroot, se
// sirve; si no, la API funciona igual (por ejemplo en desarrollo con Vite).
app.UseDefaultFiles();
app.UseStaticFiles();

// --- público: lo que ve el cliente con su código ----------------------------
var publico = app.MapGroup("/api");

publico.MapGet("/salud", () => Results.Ok(new
{
    estado = "ok",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
    hora = DateTime.UtcNow,
}));

publico.MapGet("/servicios", async (LavarDbContext db, CancellationToken ct) =>
{
    var servicios = await db.Servicios
        .Where(s => s.Activo)
        .OrderBy(s => s.Orden).ThenBy(s => s.Nombre)
        .Select(s => new { s.Id, s.Nombre, unidad = s.Unidad.ToString(), s.Precio })
        .ToListAsync(ct);
    return Results.Ok(servicios);
});

publico.MapGet("/pedidos/{codigo}", async (string codigo, LavarDbContext db, IConfiguration config, CancellationToken ct) =>
{
    var normalizado = Codigos.Normalizar(codigo);
    if (normalizado.Length == 0) return Results.NotFound();

    var pedido = await db.Pedidos
        .Include(p => p.Cliente)
        .Include(p => p.Items)
        .FirstOrDefaultAsync(p => p.Codigo == normalizado, ct);

    if (pedido is null) return Results.NotFound(new { error = "No encontramos ningún pedido con ese código." });

    return Results.Ok(new
    {
        pedido.Codigo,
        estado = pedido.Estado.ToString(),
        estadoEtiqueta = Estados.Etiqueta(pedido.Estado),
        pedido.RecibidoEn,
        pedido.PrometidoPara,
        pedido.EntregadoEn,
        pedido.RetiroADomicilio,
        cliente = PrimerNombre(pedido.Cliente?.Nombre),
        lavanderia = NombreLavanderia(config),
        total = pedido.Total,
        items = pedido.Items.Select(i => new
        {
            descripcion = i.Descripcion,
            cantidad = i.Cantidad,
            cantidadTexto = i.CantidadTexto,
            unidad = i.Unidad.ToString(),
            precioUnitario = i.PrecioUnitario,
            subtotal = i.Subtotal,
        }),
    });
}).RequireRateLimiting("publico");

// --- panel: todo lo que administra la lavandería ----------------------------
var admin = app.MapGroup("/api/admin").AddEndpointFilter<TokenDeAdmin>();

admin.MapGet("/resumen", async (LavarDbContext db, CancellationToken ct) =>
{
    var porEstado = await db.Pedidos
        .GroupBy(p => p.Estado)
        .Select(g => new { estado = g.Key, cantidad = g.Count() })
        .ToListAsync(ct);

    var hoy = DateTime.Today;
    var desde = hoy.ToUniversalTime();
    var facturadoHoy = await db.Pedidos
        .Where(p => p.RecibidoEn >= desde && p.Estado != EstadoPedido.Cancelado)
        .Include(p => p.Items)
        .ToListAsync(ct);

    return Results.Ok(new
    {
        porEstado = Estados.Todos.Select(e => new
        {
            estado = e.ToString(),
            etiqueta = Estados.Etiqueta(e),
            cantidad = porEstado.FirstOrDefault(x => x.estado == e)?.cantidad ?? 0,
        }),
        pedidosHoy = facturadoHoy.Count,
        facturadoHoy = facturadoHoy.Sum(p => p.Total),
        clientes = await db.Clientes.CountAsync(ct),
    });
});

admin.MapGet("/pedidos", async (LavarDbContext db, EstadoPedido? estado, string? q, CancellationToken ct) =>
{
    var consulta = db.Pedidos.Include(p => p.Cliente).Include(p => p.Items).AsQueryable();

    if (estado is { } filtro)
        consulta = consulta.Where(p => p.Estado == filtro);

    if (!string.IsNullOrWhiteSpace(q))
    {
        var texto = q.Trim();
        consulta = consulta.Where(p => p.Codigo.Contains(texto) || p.Cliente!.Nombre.Contains(texto)
            || (p.Cliente!.Telefono != null && p.Cliente.Telefono.Contains(texto)));
    }

    var pedidos = await consulta
        .OrderByDescending(p => p.RecibidoEn)
        .Take(200)
        .ToListAsync(ct);

    return Results.Ok(pedidos.Select(ResumenDePedido));
});

admin.MapGet("/pedidos/{id:guid}", async (Guid id, LavarDbContext db, CancellationToken ct) =>
{
    var pedido = await db.Pedidos.Include(p => p.Cliente).Include(p => p.Items)
        .FirstOrDefaultAsync(p => p.Id == id, ct);
    return pedido is null ? Results.NotFound() : Results.Ok(DetalleDePedido(pedido));
});

admin.MapPost("/pedidos", async (NuevoPedido entrada, LavarDbContext db, CancellationToken ct) =>
{
    var items = entrada.Items ?? [];
    if (items.Count == 0)
        throw new ReglaDeNegocioException("Un pedido necesita al menos un servicio.");
    if (items.Count > 50)
        throw new ReglaDeNegocioException("Un pedido no puede tener más de 50 ítems.");
    foreach (var item in items)
    {
        if (item.Cantidad <= 0 || item.Cantidad > 999)
            throw new ReglaDeNegocioException("La cantidad de cada ítem tiene que estar entre 1 y 999.");
    }
    // SQLite no aplica los MaxLength del modelo: se validan acá para que no entre
    // texto sin límite desde el panel.
    if (entrada.ClienteNombre is { Length: > 120 })
        throw new ReglaDeNegocioException("El nombre del cliente no puede superar los 120 caracteres.");
    if (entrada.ClienteTelefono is { Length: > 40 })
        throw new ReglaDeNegocioException("El teléfono no puede superar los 40 caracteres.");
    if (entrada.Direccion is { Length: > 200 })
        throw new ReglaDeNegocioException("La dirección no puede superar los 200 caracteres.");
    if (entrada.Notas is { Length: > 1000 })
        throw new ReglaDeNegocioException("Las notas no pueden superar los 1000 caracteres.");

    var cliente = entrada.ClienteId is { } id
        ? await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct)
          ?? throw new ReglaDeNegocioException("Ese cliente no existe.")
        : await ClienteDelPedidoAsync(db, entrada, ct);

    var servicios = await db.Servicios.ToListAsync(ct);
    var codigos = await db.Pedidos.Select(p => p.Codigo).ToListAsync(ct);

    var pedido = new Pedido
    {
        Codigo = Codigos.Generar(codigos.ToHashSet()),
        Cliente = cliente,
        RetiroADomicilio = entrada.RetiroADomicilio,
        Direccion = string.IsNullOrWhiteSpace(entrada.Direccion) ? null : entrada.Direccion.Trim(),
        PrometidoPara = entrada.PrometidoPara,
        Notas = string.IsNullOrWhiteSpace(entrada.Notas) ? null : entrada.Notas.Trim(),
    };

    foreach (var item in items)
    {
        var servicio = servicios.FirstOrDefault(s => s.Id == item.ServicioId)
            ?? throw new ReglaDeNegocioException("Uno de los servicios del pedido ya no existe.");
        pedido.Agregar(servicio, item.Cantidad);
    }

    db.Pedidos.Add(pedido);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/admin/pedidos/{pedido.Id}", DetalleDePedido(pedido));
});

admin.MapPost("/pedidos/{id:guid}/estado", async (Guid id, CambioDeEstado cambio, LavarDbContext db, CancellationToken ct) =>
{
    var pedido = await db.Pedidos.Include(p => p.Cliente).Include(p => p.Items)
        .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new ReglaDeNegocioException("Ese pedido no existe.");

    pedido.CambiarEstado(cambio.Estado);
    await db.SaveChangesAsync(ct);

    return Results.Ok(DetalleDePedido(pedido));
});

admin.MapGet("/pedidos/{id:guid}/aviso", async (Guid id, LavarDbContext db, IConfiguration config, CancellationToken ct) =>
{
    var pedido = await db.Pedidos.Include(p => p.Cliente).Include(p => p.Items)
        .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new ReglaDeNegocioException("Ese pedido no existe.");

    var mensaje = Avisos.MensajePedido(pedido, NombreLavanderia(config));
    return Results.Ok(new
    {
        mensaje,
        telefono = pedido.Cliente?.Telefono,
        whatsapp = Avisos.LinkWhatsApp(pedido.Cliente?.Telefono, mensaje),
    });
});

admin.MapGet("/clientes", async (LavarDbContext db, string? q, CancellationToken ct) =>
{
    var consulta = db.Clientes.AsQueryable();
    if (!string.IsNullOrWhiteSpace(q))
    {
        var texto = q.Trim();
        consulta = consulta.Where(c => c.Nombre.Contains(texto) || (c.Telefono != null && c.Telefono.Contains(texto)));
    }

    var clientes = await consulta
        .OrderBy(c => c.Nombre)
        .Take(200)
        .Select(c => new
        {
            c.Id,
            c.Nombre,
            c.Telefono,
            c.Email,
            c.Notas,
            c.CreadoEn,
            pedidos = c.Pedidos.Count,
            enCurso = c.Pedidos.Count(p => p.Estado != EstadoPedido.Entregado && p.Estado != EstadoPedido.Cancelado),
        })
        .ToListAsync(ct);

    return Results.Ok(clientes);
});

admin.MapPost("/clientes", async (ClienteEntrada entrada, LavarDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(entrada.Nombre))
        throw new ReglaDeNegocioException("El cliente necesita un nombre.");

    var cliente = new Cliente
    {
        Nombre = entrada.Nombre.Trim(),
        Telefono = Limpio(entrada.Telefono),
        Email = Limpio(entrada.Email),
        Notas = Limpio(entrada.Notas),
    };

    db.Clientes.Add(cliente);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/admin/clientes/{cliente.Id}", new { cliente.Id, cliente.Nombre });
});

admin.MapPatch("/clientes/{id:guid}", async (Guid id, ClienteEntrada entrada, LavarDbContext db, CancellationToken ct) =>
{
    var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new ReglaDeNegocioException("Ese cliente no existe.");

    if (!string.IsNullOrWhiteSpace(entrada.Nombre)) cliente.Nombre = entrada.Nombre.Trim();
    cliente.Telefono = Limpio(entrada.Telefono) ?? cliente.Telefono;
    cliente.Email = Limpio(entrada.Email) ?? cliente.Email;
    cliente.Notas = Limpio(entrada.Notas) ?? cliente.Notas;

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { cliente.Id, cliente.Nombre });
});

admin.MapGet("/servicios", async (LavarDbContext db, CancellationToken ct) =>
{
    var servicios = await db.Servicios
        .OrderBy(s => s.Orden).ThenBy(s => s.Nombre)
        .Select(s => new { s.Id, s.Nombre, unidad = s.Unidad.ToString(), s.Precio, s.Activo, s.Orden })
        .ToListAsync(ct);
    return Results.Ok(servicios);
});

admin.MapPost("/servicios", async (ServicioEntrada entrada, LavarDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(entrada.Nombre))
        throw new ReglaDeNegocioException("El servicio necesita un nombre.");
    if (entrada.Precio < 0)
        throw new ReglaDeNegocioException("El precio no puede ser negativo.");

    var nombre = entrada.Nombre.Trim();
    if (await db.Servicios.AnyAsync(s => s.Nombre == nombre, ct))
        throw new ReglaDeNegocioException("Ya hay un servicio con ese nombre.");

    var servicio = new Servicio
    {
        Nombre = nombre,
        Unidad = entrada.Unidad,
        Precio = entrada.Precio,
        Orden = entrada.Orden ?? 100,
        Activo = entrada.Activo ?? true,
    };

    db.Servicios.Add(servicio);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/admin/servicios/{servicio.Id}", new { servicio.Id, servicio.Nombre });
});

admin.MapPatch("/servicios/{id:guid}", async (Guid id, ServicioEntrada entrada, LavarDbContext db, CancellationToken ct) =>
{
    var servicio = await db.Servicios.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new ReglaDeNegocioException("Ese servicio no existe.");

    if (!string.IsNullOrWhiteSpace(entrada.Nombre)) servicio.Nombre = entrada.Nombre.Trim();
    if (entrada.Precio < 0) throw new ReglaDeNegocioException("El precio no puede ser negativo.");
    if (entrada.Precio > 0) servicio.Precio = entrada.Precio;
    servicio.Unidad = entrada.Unidad;
    servicio.Activo = entrada.Activo ?? servicio.Activo;
    servicio.Orden = entrada.Orden ?? servicio.Orden;

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { servicio.Id, servicio.Nombre, activo = servicio.Activo });
});

app.MapFallback(async (HttpContext contexto) =>
{
    // Una ruta /api que no existe es un 404, no la SPA: si devolviera HTML,
    // cualquier cliente que sondee la API recibiría 200 con una página.
    if (contexto.Request.Path.StartsWithSegments("/api"))
    {
        contexto.Response.StatusCode = StatusCodes.Status404NotFound;
        await contexto.Response.WriteAsJsonAsync(new { error = "No existe ese endpoint." });
        return;
    }
    contexto.Response.ContentType = "text/html; charset=utf-8";
    await contexto.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html"));
});

app.Run();

// --- ayudas ------------------------------------------------------------------

static string NombreLavanderia(IConfiguration config) =>
    config["Lavanderia:Nombre"] is { Length: > 0 } nombre ? nombre : "La lavandería";

/// <summary>
/// Un booleano escrito a mano. `GetValue&lt;bool&gt;` explota con "1", que es lo que
/// todo el mundo pone en un docker-compose o en el panel de un hosting.
/// </summary>
static bool Verdadero(string? valor) =>
    valor?.Trim().ToLowerInvariant() is "1" or "true" or "si" or "sí" or "yes" or "on";

static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

static string? PrimerNombre(string? nombre)
{
    if (string.IsNullOrWhiteSpace(nombre)) return null;
    return nombre.Trim().Split(' ')[0];
}

static async Task<Cliente> ClienteDelPedidoAsync(LavarDbContext db, NuevoPedido entrada, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(entrada.ClienteNombre))
        throw new ReglaDeNegocioException("Elegí un cliente o escribí el nombre de uno nuevo.");

    var nombre = entrada.ClienteNombre.Trim();
    var telefono = Limpio(entrada.ClienteTelefono);

    // El mismo teléfono es el mismo cliente: evita duplicados al cargar rápido.
    var existente = telefono is null
        ? await db.Clientes.FirstOrDefaultAsync(c => c.Nombre == nombre && c.Telefono == null, ct)
        : await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono, ct);

    return existente ?? new Cliente { Nombre = nombre, Telefono = telefono };
}

static object ResumenDePedido(Pedido pedido) => new
{
    pedido.Id,
    pedido.Codigo,
    estado = pedido.Estado.ToString(),
    estadoEtiqueta = Estados.Etiqueta(pedido.Estado),
    pedido.RecibidoEn,
    pedido.PrometidoPara,
    pedido.EntregadoEn,
    pedido.RetiroADomicilio,
    clienteNombre = pedido.Cliente?.Nombre,
    clienteTelefono = pedido.Cliente?.Telefono,
    total = pedido.Total,
    items = pedido.Items.Count,
};

static object DetalleDePedido(Pedido pedido) => new
{
    pedido.Id,
    pedido.Codigo,
    estado = pedido.Estado.ToString(),
    estadoEtiqueta = Estados.Etiqueta(pedido.Estado),
    pedido.RecibidoEn,
    pedido.PrometidoPara,
    pedido.EntregadoEn,
    pedido.RetiroADomicilio,
    pedido.Direccion,
    pedido.Notas,
    total = pedido.Total,
    cliente = new
    {
        id = pedido.Cliente?.Id,
        nombre = pedido.Cliente?.Nombre,
        telefono = pedido.Cliente?.Telefono,
    },
    items = pedido.Items.Select(i => new
    {
        i.Id,
        i.ServicioId,
        i.Descripcion,
        cantidad = i.Cantidad,
        cantidadTexto = i.CantidadTexto,
        unidad = i.Unidad.ToString(),
        i.PrecioUnitario,
        subtotal = i.Subtotal,
    }),
};

/// <summary>
/// Un solo token para todo el panel: la lavandería es un negocio chico, no hay
/// usuarios con roles que administrar. Se compara en tiempo constante para no
/// filtrar el token por la diferencia de tiempos.
/// </summary>
sealed class TokenDeAdmin(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var esperado = config["Admin:Token"] ?? Environment.GetEnvironmentVariable("LAVAR_ADMIN_TOKEN");
        if (string.IsNullOrWhiteSpace(esperado))
        {
            return Results.Json(
                new { error = "Falta configurar el token del panel (LAVAR_ADMIN_TOKEN)." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var recibido = contexto.HttpContext.Request.Headers["X-Admin-Token"].ToString();
        if (!IgualesEnTiempoConstante(recibido, esperado))
            return Results.Json(new { error = "Token inválido." }, statusCode: StatusCodes.Status401Unauthorized);

        return await siguiente(contexto);
    }

    private static bool IgualesEnTiempoConstante(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}

/// <summary>
/// Los tests de integración levantan la API real con <see cref="WebApplicationFactory{TEntryPoint}"/>:
/// para eso el tipo generado por el `Program` de nivel superior tiene que ser público.
/// </summary>
public partial class Program;

