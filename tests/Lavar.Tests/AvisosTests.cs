using Lavar.Core;
using Lavar.Core.Modelos;

namespace Lavar.Tests;

public class AvisosTests
{
    [Theory]
    [InlineData("011 15-5555-1234", "5491155551234")]
    [InlineData("11 15 5555 1234", "5491155551234")]
    [InlineData("11 5555 1234", "5491155551234")]
    [InlineData("1155551234", "5491155551234")]
    [InlineData("+54 9 11 5555 1234", "5491155551234")]
    [InlineData("54 9 11 5555-1234", "5491155551234")]
    [InlineData("(011) 15.5555.1234", "5491155551234")]
    public void Deja_el_telefono_en_el_formato_que_pide_whatsapp(string entrada, string esperado)
    {
        Assert.Equal(esperado, Avisos.NormalizarTelefono(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin numero")]
    [InlineData("5555-1234")] // sin área no se puede saber de dónde es
    public void Un_telefono_incompleto_devuelve_null_en_vez_de_inventar(string? entrada)
    {
        Assert.Null(Avisos.NormalizarTelefono(entrada));
    }

    [Fact]
    public void El_link_de_whatsapp_lleva_el_mensaje_codificado()
    {
        var link = Avisos.LinkWhatsApp("11 5555 1234", "Hola, tu pedido ABC123 está listo.");

        Assert.NotNull(link);
        Assert.StartsWith("https://wa.me/5491155551234?text=", link);
        Assert.DoesNotContain(" ", link);
        Assert.Contains("ABC123", Uri.UnescapeDataString(link!));
    }

    [Fact]
    public void Sin_telefono_no_hay_link()
    {
        Assert.Null(Avisos.LinkWhatsApp(null, "cualquier cosa"));
        Assert.Null(Avisos.LinkWhatsApp("5555", "cualquier cosa"));
    }

    [Fact]
    public void El_mensaje_dice_el_codigo_el_estado_y_la_lavanderia()
    {
        var pedido = new Pedido
        {
            Codigo = "K7M2QP",
            Cliente = new Cliente { Nombre = "Ana Gómez" },
            Estado = EstadoPedido.Listo,
            PrometidoPara = new DateOnly(2026, 11, 12),
        };

        var mensaje = Avisos.MensajePedido(pedido, "Lavadero Rivadavia");

        Assert.Contains("Ana", mensaje);
        Assert.Contains("K7M2QP", mensaje);
        Assert.Contains("listo", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12/11", mensaje);
        Assert.Contains("Lavadero Rivadavia", mensaje);
    }

    [Fact]
    public void Un_pedido_para_llevar_a_domicilio_lo_dice()
    {
        var pedido = new Pedido
        {
            Codigo = "K7M2QP",
            Cliente = new Cliente { Nombre = "Ana" },
            Estado = EstadoPedido.Listo,
            RetiroADomicilio = true,
        };

        Assert.Contains("domicilio", Avisos.MensajePedido(pedido));
    }

    [Fact]
    public void Un_pedido_cancelado_no_habla_de_fechas_de_entrega()
    {
        var pedido = new Pedido
        {
            Codigo = "K7M2QP",
            Cliente = new Cliente { Nombre = "Ana" },
            PrometidoPara = new DateOnly(2026, 11, 12),
        };
        pedido.CambiarEstado(EstadoPedido.Cancelado);

        var mensaje = Avisos.MensajePedido(pedido);

        Assert.Contains("cancelado", mensaje);
        Assert.DoesNotContain("12/11", mensaje);
    }
}
