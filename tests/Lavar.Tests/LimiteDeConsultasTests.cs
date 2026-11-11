using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lavar.Tests;

/// <summary>
/// El código es la única llave del pedido: si se puede probar sin límite, alguien
/// con un script puede ir pegándole a códigos al azar hasta dar con el de otro
/// cliente. Este test levanta la app con un límite chico y verifica el 429.
/// </summary>
public class LimiteDeConsultasTests : WebApplicationFactory<Program>
{
    private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"lavar-limite-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Lavar", $"Data Source={_archivo}");
        builder.UseSetting("Admin:Token", "token-de-prueba");
        builder.UseSetting("Lavanderia:Demo", "false");
        builder.UseSetting("RateLimit:PublicoPorMinuto", "2");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try { File.Delete(_archivo); } catch (IOException) { /* SQLite puede tener el archivo tomado un instante */ }
    }
}

public class LimiteDeConsultasTest(LimiteDeConsultasTests laboratorio) : IClassFixture<LimiteDeConsultasTests>
{
    [Fact]
    public async Task Consultar_codigos_tiene_limite_por_minuto()
    {
        var cliente = laboratorio.CreateClient();

        var primera = await cliente.GetAsync("/api/pedidos/ZZZZZZ");
        var segunda = await cliente.GetAsync("/api/pedidos/ZZZZZZ");
        var tercera = await cliente.GetAsync("/api/pedidos/ZZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, primera.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, tercera.StatusCode);
    }

    [Fact]
    public async Task El_panel_no_esta_limitado()
    {
        var cliente = laboratorio.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Admin-Token", "token-de-prueba");

        for (var i = 0; i < 5; i++)
        {
            var respuesta = await cliente.GetAsync("/api/admin/resumen");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }
}
