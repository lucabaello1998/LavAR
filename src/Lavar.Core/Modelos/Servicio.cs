namespace Lavar.Core.Modelos;

/// <summary>
/// Algo que la lavandería cobra: por kilo, por colchón, por par de zapatillas,
/// planchado, tintorería. El precio es el de la lista: cada pedido guarda su
/// propia copia (<see cref="PedidoItem.PrecioUnitario"/>) para que un ajuste de
/// precios no reescriba la historia.
/// </summary>
public class Servicio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Nombre { get; set; }

    public Unidad Unidad { get; set; } = Unidad.Unidad;

    /// <summary>Precio por <see cref="Unidad"/>, en pesos.</summary>
    public decimal Precio { get; set; }

    /// <summary>Los servicios viejos se desactivan en vez de borrarse: siguen apareciendo en los pedidos.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Orden en la lista de la pantalla de nuevo pedido.</summary>
    public int Orden { get; set; }
}
