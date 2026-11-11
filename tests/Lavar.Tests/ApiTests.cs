using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Lavar.Tests;

/// <summary>
/// Levanta la API de verdad (con SQLite en un archivo temporal) y le pega por
/// HTTP: si el ruteo, el token o la serialización se rompen, los tests lo ven.
/// </summary>
public class LaboratorioDePruebas : WebApplicationFactory<Program>
{
    public const string Token = "token-de-prueba";

    private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"lavar-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // `UseSetting` es lo que llega al `builder.Configuration` del Program de
        // nivel superior; `ConfigureAppConfiguration` corre más tarde y la cadena
        // de conexión ya se leyó, así que los tests terminaban usando lavar.db.
        builder.UseSetting("ConnectionStrings:Lavar", $"Data Source={_archivo}");
        builder.UseSetting("Admin:Token", Token);
        builder.UseSetting("Lavanderia:Nombre", "Lavandería de prueba");
        builder.UseSetting("Lavanderia:Demo", "false");
    }

    /// <summary>Un cliente con el token del panel.</summary>
    public HttpClient Panel()
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Admin-Token", Token);
        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try { File.Delete(_archivo); } catch (IOException) { /* SQLite puede tener el archivo tomado un instante */ }
    }
}

public class ApiTests(LaboratorioDePruebas laboratorio) : IClassFixture<LaboratorioDePruebas>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private static async Task<Guid> PrimerServicioAsync(HttpClient panel)
    {
        var servicios = await JsonAsync(await panel.GetAsync("/api/admin/servicios"));
        return servicios[0].GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CrearPedidoAsync(HttpClient panel, decimal kilos = 3.5m, string? codigoCliente = null, string? telefono = null)
    {
        var servicioId = await PrimerServicioAsync(panel);
        var respuesta = await panel.PostAsJsonAsync("/api/admin/pedidos", new
        {
            ClienteNombre = codigoCliente ?? "Ana Gómez",
            // Teléfono propio por pedido: el mismo número es el mismo cliente, y
            // eso es a propósito (evita cargar dos veces a la misma persona).
            ClienteTelefono = telefono ?? "11 5555 1234",
            RetiroADomicilio = true,
            Direccion = "Rivadavia 5000",
            PrometidoPara = "2026-11-12",
            Notas = "mancha en el mantel",
            Items = new[] { new { ServicioId = servicioId, Cantidad = kilos } },
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await JsonAsync(respuesta);
    }

    [Fact]
    public async Task El_health_responde_ok()
    {
        var respuesta = await laboratorio.CreateClient().GetAsync("/api/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await JsonAsync(respuesta);
        Assert.Equal("ok", json.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task El_panel_sin_token_no_deja_entrar()
    {
        var respuesta = await laboratorio.CreateClient().GetAsync("/api/admin/pedidos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_panel_con_un_token_equivocado_no_deja_entrar()
    {
        using var cliente = laboratorio.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Admin-Token", "otro-token");

        var respuesta = await cliente.GetAsync("/api/admin/pedidos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Los_servicios_publicos_son_los_activos()
    {
        var json = await JsonAsync(await laboratorio.CreateClient().GetAsync("/api/servicios"));

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.True(json.GetArrayLength() >= 8);
        Assert.All(json.EnumerateArray(), s => Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("nombre").GetString())));
    }

    [Fact]
    public async Task Crear_un_pedido_le_asigna_un_codigo_y_calcula_el_total()
    {
        var panel = laboratorio.Panel();
        var pedido = await CrearPedidoAsync(panel, 2m);

        var codigo = pedido.GetProperty("codigo").GetString();
        Assert.NotNull(codigo);
        Assert.Equal(6, codigo!.Length);
        Assert.Equal("Recibido", pedido.GetProperty("estado").GetString());
        Assert.True(pedido.GetProperty("total").GetDecimal() > 0);
        Assert.Equal("Ana Gómez", pedido.GetProperty("cliente").GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task El_cliente_consulta_su_pedido_con_el_codigo_en_minusculas()
    {
        var pedido = await CrearPedidoAsync(laboratorio.Panel());
        var codigo = pedido.GetProperty("codigo").GetString()!;

        var respuesta = await laboratorio.CreateClient().GetAsync($"/api/pedidos/{codigo.ToLowerInvariant()}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await JsonAsync(respuesta);
        Assert.Equal("Recibido", json.GetProperty("estado").GetString());
        Assert.Equal("Recibido", json.GetProperty("estadoEtiqueta").GetString());
        Assert.Equal("Lavandería de prueba", json.GetProperty("lavanderia").GetString());
        Assert.Equal("Ana", json.GetProperty("cliente").GetString());
        Assert.Equal("3,5 kg", json.GetProperty("items")[0].GetProperty("cantidadTexto").GetString());
    }

    [Fact]
    public async Task Un_codigo_que_no_existe_da_404_y_no_un_500()
    {
        var respuesta = await laboratorio.CreateClient().GetAsync("/api/pedidos/ZZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_flujo_completo_termina_entregado()
    {
        var panel = laboratorio.Panel();
        var pedido = await CrearPedidoAsync(panel);
        var id = pedido.GetProperty("id").GetGuid();

        foreach (var estado in new[] { "EnProceso", "Listo", "Entregado" })
        {
            var respuesta = await panel.PostAsJsonAsync($"/api/admin/pedidos/{id}/estado", new { Estado = estado });
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        var final = await JsonAsync(await laboratorio.CreateClient().GetAsync($"/api/pedidos/{pedido.GetProperty("codigo").GetString()}"));
        Assert.Equal("Entregado", final.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null, final.GetProperty("entregadoEn").ValueKind);
    }

    [Fact]
    public async Task Entregar_un_pedido_recien_recibido_da_400_con_el_motivo()
    {
        var panel = laboratorio.Panel();
        var pedido = await CrearPedidoAsync(panel);

        var respuesta = await panel.PostAsJsonAsync(
            $"/api/admin/pedidos/{pedido.GetProperty("id").GetGuid()}/estado",
            new { Estado = "Entregado" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var json = await JsonAsync(respuesta);
        Assert.Contains("listo", json.GetProperty("error").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_pedido_sin_servicios_se_rechaza()
    {
        var respuesta = await laboratorio.Panel().PostAsJsonAsync("/api/admin/pedidos", new
        {
            ClienteNombre = "Sin items",
            Items = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_pedido_con_un_servicio_inexistente_se_rechaza()
    {
        var respuesta = await laboratorio.Panel().PostAsJsonAsync("/api/admin/pedidos", new
        {
            ClienteNombre = "Ana",
            Items = new[] { new { ServicioId = Guid.NewGuid(), Cantidad = 1 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_aviso_trae_el_mensaje_y_el_link_de_whatsapp()
    {
        var panel = laboratorio.Panel();
        var pedido = await CrearPedidoAsync(panel);
        var id = pedido.GetProperty("id").GetGuid();
        await panel.PostAsJsonAsync($"/api/admin/pedidos/{id}/estado", new { Estado = "EnProceso" });
        await panel.PostAsJsonAsync($"/api/admin/pedidos/{id}/estado", new { Estado = "Listo" });

        var aviso = await JsonAsync(await panel.GetAsync($"/api/admin/pedidos/{id}/aviso"));

        var mensaje = aviso.GetProperty("mensaje").GetString()!;
        Assert.Contains(pedido.GetProperty("codigo").GetString()!, mensaje);
        Assert.Contains("listo", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://wa.me/5491155551234?text=", aviso.GetProperty("whatsapp").GetString());
    }

    [Fact]
    public async Task El_resumen_cuenta_por_estado_y_lo_del_dia()
    {
        await CrearPedidoAsync(laboratorio.Panel());

        var resumen = await JsonAsync(await laboratorio.Panel().GetAsync("/api/admin/resumen"));

        Assert.True(resumen.GetProperty("pedidosHoy").GetInt32() >= 1);
        Assert.True(resumen.GetProperty("facturadoHoy").GetDecimal() > 0);
        Assert.Equal(5, resumen.GetProperty("porEstado").GetArrayLength());
    }

    [Fact]
    public async Task El_buscador_encuentra_por_codigo_y_por_nombre()
    {
        var panel = laboratorio.Panel();
        var pedido = await CrearPedidoAsync(panel, codigoCliente: "Beto Suárez", telefono: "11 4444 9876");
        var codigo = pedido.GetProperty("codigo").GetString()!;

        var porNombre = await JsonAsync(await panel.GetAsync("/api/admin/pedidos?q=Beto"));
        var porCodigo = await JsonAsync(await panel.GetAsync($"/api/admin/pedidos?q={codigo}"));

        Assert.Contains(porNombre.EnumerateArray(), p => p.GetProperty("clienteNombre").GetString() == "Beto Suárez");
        Assert.Contains(porCodigo.EnumerateArray(), p => p.GetProperty("codigo").GetString() == codigo);
    }

    [Fact]
    public async Task No_se_pueden_cargar_dos_servicios_con_el_mismo_nombre()
    {
        var panel = laboratorio.Panel();
        var nuevo = new { Nombre = "Servicio único", Unidad = "Kilo", Precio = 1000m };

        var primera = await panel.PostAsJsonAsync("/api/admin/servicios", nuevo);
        Assert.True(primera.StatusCode == HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());

        var segunda = await panel.PostAsJsonAsync("/api/admin/servicios", nuevo);
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
        Assert.Contains("ese nombre", await segunda.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_servicio_desactivado_no_aparece_en_la_lista_publica()
    {
        var panel = laboratorio.Panel();
        var creado = await JsonAsync(await panel.PostAsJsonAsync("/api/admin/servicios",
            new { Nombre = "Solo para probar", Unidad = "Unidad", Precio = 500m }));
        var id = creado.GetProperty("id").GetGuid();

        await panel.PatchAsJsonAsync($"/api/admin/servicios/{id}",
            new { Nombre = "Solo para probar", Unidad = "Unidad", Precio = 500m, Activo = false });

        var publicos = await JsonAsync(await laboratorio.CreateClient().GetAsync("/api/servicios"));
        Assert.DoesNotContain(publicos.EnumerateArray(), s => s.GetProperty("nombre").GetString() == "Solo para probar");
    }
}
