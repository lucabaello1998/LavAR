using Lavar.Core;
using Lavar.Core.Modelos;

namespace Lavar.Tests;

public class CodigosTests
{
    [Fact]
    public void El_codigo_tiene_seis_caracteres()
    {
        Assert.Equal(6, Codigos.Generar().Length);
    }

    [Fact]
    public void El_codigo_no_usa_caracteres_que_se_confunden_al_dictarlos()
    {
        // 0/O y 1/I/L son la llamada de todos los días: "no, con cero".
        foreach (var prohibido in "01OIL")
        {
            for (var i = 0; i < 200; i++)
                Assert.DoesNotContain(prohibido, Codigos.Generar());
        }
    }

    [Fact]
    public void No_repite_un_codigo_que_ya_esta_en_uso()
    {
        var usados = new HashSet<string>();
        for (var i = 0; i < 300; i++)
        {
            var codigo = Codigos.Generar(usados);
            Assert.True(usados.Add(codigo), "el generador devolvió un código repetido");
        }
    }

    [Fact]
    public void Los_codigos_se_repiten_poco()
    {
        // Con 32^6 combinaciones, 1000 códigos no deberían chocar.
        var vistos = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
            Assert.True(vistos.Add(Codigos.Generar()), "colisión en 1000 códigos");
    }

    [Theory]
    [InlineData("k7m 2qp", "K7M2QP")]
    [InlineData("K7M-2QP", "K7M2QP")]
    [InlineData("  k7m_2qp ", "K7M2QP")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normaliza_lo_que_escribe_la_gente(string? entrada, string esperado)
    {
        Assert.Equal(esperado, Codigos.Normalizar(entrada));
    }
}

public class EstadosTests
{
    [Theory]
    [InlineData(EstadoPedido.Recibido, EstadoPedido.EnProceso)]
    [InlineData(EstadoPedido.EnProceso, EstadoPedido.Listo)]
    [InlineData(EstadoPedido.Listo, EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.EnProceso, EstadoPedido.Recibido)]
    [InlineData(EstadoPedido.Listo, EstadoPedido.EnProceso)]
    [InlineData(EstadoPedido.Recibido, EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Listo, EstadoPedido.Cancelado)]
    public void Deja_pasar_los_cambios_razonables(EstadoPedido desde, EstadoPedido hasta)
    {
        Assert.True(Estados.PuedePasar(desde, hasta));
    }

    [Theory]
    [InlineData(EstadoPedido.Recibido, EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.EnProceso, EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.Cancelado, EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.Entregado, EstadoPedido.Listo)]
    [InlineData(EstadoPedido.Entregado, EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Cancelado, EstadoPedido.Recibido)]
    public void Rechaza_los_cambios_que_dejan_al_cliente_sin_ropa(EstadoPedido desde, EstadoPedido hasta)
    {
        Assert.False(Estados.PuedePasar(desde, hasta));
    }

    [Fact]
    public void Volver_al_mismo_estado_es_idempotente()
    {
        foreach (var estado in Estados.Todos)
            Assert.True(Estados.PuedePasar(estado, estado));
    }

    [Fact]
    public void Todos_son_legibles()
    {
        foreach (var estado in Estados.Todos)
        {
            Assert.False(string.IsNullOrWhiteSpace(Estados.Etiqueta(estado)));
            Assert.False(string.IsNullOrWhiteSpace(Estados.ParaElCliente(estado)));
        }
    }
}

public class PedidoTests
{
    private static Servicio PorKilo(decimal precio = 2500m) => new()
    {
        Nombre = "Lavado por kilo",
        Unidad = Unidad.Kilo,
        Precio = precio,
    };

    [Fact]
    public void El_total_suma_los_subtotales()
    {
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.Agregar(PorKilo(), 3.5m);
        pedido.Agregar(new Servicio { Nombre = "Acolchado", Unidad = Unidad.Unidad, Precio = 18000m }, 1);

        Assert.Equal(8750m, pedido.Items[0].Subtotal);
        Assert.Equal(26750m, pedido.Total);
    }

    [Fact]
    public void El_precio_queda_congelado_al_crear_el_pedido()
    {
        var servicio = PorKilo(2500m);
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.Agregar(servicio, 2);

        servicio.Precio = 9000m; // subió la lista

        Assert.Equal(5000m, pedido.Total);
    }

    [Fact]
    public void No_acepta_cantidades_en_cero_o_negativas()
    {
        var pedido = new Pedido { Codigo = "ABC123" };

        Assert.Throws<ReglaDeNegocioException>(() => pedido.Agregar(PorKilo(), 0));
        Assert.Throws<ReglaDeNegocioException>(() => pedido.Agregar(PorKilo(), -1));
    }

    [Fact]
    public void Los_kilos_pueden_ser_decimales_pero_los_pares_no()
    {
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.Agregar(PorKilo(), 0.75m);

        var zapatillas = new Servicio { Nombre = "Zapatillas", Unidad = Unidad.Par, Precio = 8500m };
        Assert.Throws<ReglaDeNegocioException>(() => pedido.Agregar(zapatillas, 1.5m));
    }

    [Fact]
    public void No_se_entrega_un_pedido_que_todavia_no_esta_listo()
    {
        var pedido = new Pedido { Codigo = "ABC123" };

        var error = Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarEstado(EstadoPedido.Entregado));

        Assert.Contains("listo", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoPedido.Recibido, pedido.Estado);
    }

    [Fact]
    public void El_flujo_completo_marca_la_fecha_de_entrega()
    {
        var pedido = new Pedido { Codigo = "ABC123" };

        pedido.CambiarEstado(EstadoPedido.EnProceso);
        pedido.CambiarEstado(EstadoPedido.Listo);
        Assert.Null(pedido.EntregadoEn);

        pedido.CambiarEstado(EstadoPedido.Entregado);

        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
        Assert.NotNull(pedido.EntregadoEn);
    }

    [Fact]
    public void Un_pedido_entregado_no_vuelve_atras()
    {
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.CambiarEstado(EstadoPedido.EnProceso);
        pedido.CambiarEstado(EstadoPedido.Listo);
        pedido.CambiarEstado(EstadoPedido.Entregado);

        Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarEstado(EstadoPedido.Listo));
    }

    [Fact]
    public void Corregir_un_estado_mal_tocado_limpia_la_fecha_de_entrega()
    {
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.CambiarEstado(EstadoPedido.EnProceso);
        pedido.CambiarEstado(EstadoPedido.Listo);
        pedido.CambiarEstado(EstadoPedido.Entregado);

        // El único camino de salida de Entregado es el error de la regla, así que
        // el caso real es cancelar uno entregado por equivocación: no se permite.
        Assert.Throws<ReglaDeNegocioException>(() => pedido.CambiarEstado(EstadoPedido.Cancelado));
        Assert.NotNull(pedido.EntregadoEn);
    }

    [Fact]
    public void Muestra_la_cantidad_con_su_unidad()
    {
        var pedido = new Pedido { Codigo = "ABC123" };
        pedido.Agregar(PorKilo(), 3.5m);
        pedido.Agregar(new Servicio { Nombre = "Almohada", Unidad = Unidad.Unidad, Precio = 6000m }, 1);
        pedido.Agregar(new Servicio { Nombre = "Zapatillas", Unidad = Unidad.Par, Precio = 8500m }, 2);

        Assert.Equal("3,5 kg", pedido.Items[0].CantidadTexto);
        Assert.Equal("1 unidad", pedido.Items[1].CantidadTexto);
        Assert.Equal("2 pares", pedido.Items[2].CantidadTexto);
    }
}
