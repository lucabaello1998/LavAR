namespace Lavar.Core.Modelos;

/// <summary>
/// En qué punto está un pedido. Es lineal salvo por <see cref="Cancelado"/>: el
/// cliente lleva la ropa (Recibido), la lavandería la trabaja (EnProceso), avisa
/// que está (Listo) y el cliente la retira (Entregado).
/// </summary>
public enum EstadoPedido
{
    Recibido,
    EnProceso,
    Listo,
    Entregado,
    Cancelado,
}

public static class Estados
{
    /// <summary>
    /// Si se puede pasar de <paramref name="desde"/> a <paramref name="hasta"/>.
    ///
    /// Reglas, pensadas para un mostrador real:
    /// - <see cref="EstadoPedido.Entregado"/> y <see cref="EstadoPedido.Cancelado"/>
    ///   son finales: de ahí no se sale.
    /// - A Entregado solo se llega desde Listo: entregar algo que todavía no está
    ///   listo es el error que deja al cliente sin ropa.
    /// - Entre los demás se puede saltar para adelante o corregir para atrás
    ///   (si tocaron "Listo" por error, vuelve a EnProceso sin perder el pedido).
    /// </summary>
    public static bool PuedePasar(EstadoPedido desde, EstadoPedido hasta)
    {
        if (desde == hasta) return true;
        if (EsFinal(desde)) return false;
        if (hasta == EstadoPedido.Entregado) return desde == EstadoPedido.Listo;
        return true;
    }

    public static bool EsFinal(EstadoPedido estado) =>
        estado is EstadoPedido.Entregado or EstadoPedido.Cancelado;

    /// <summary>El texto que ve el cliente y la lavandería.</summary>
    public static string Etiqueta(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Recibido => "Recibido",
        EstadoPedido.EnProceso => "En proceso",
        EstadoPedido.Listo => "Listo para retirar",
        EstadoPedido.Entregado => "Entregado",
        EstadoPedido.Cancelado => "Cancelado",
        _ => estado.ToString(),
    };

    /// <summary>Una frase corta para el aviso al cliente.</summary>
    public static string ParaElCliente(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Recibido => "ya está en la lavandería",
        EstadoPedido.EnProceso => "está en proceso",
        EstadoPedido.Listo => "está listo para retirar",
        EstadoPedido.Entregado => "fue entregado. ¡Gracias!",
        EstadoPedido.Cancelado => "fue cancelado",
        _ => "está en curso",
    };

    public static IReadOnlyList<EstadoPedido> Todos { get; } = new[]
    {
        EstadoPedido.Recibido,
        EstadoPedido.EnProceso,
        EstadoPedido.Listo,
        EstadoPedido.Entregado,
        EstadoPedido.Cancelado,
    };
}
