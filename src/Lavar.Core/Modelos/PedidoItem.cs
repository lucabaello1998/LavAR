namespace Lavar.Core.Modelos;

/// <summary>Una línea del pedido: qué servicio y cuánto.</summary>
public class PedidoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PedidoId { get; set; }

    public Guid ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    /// <summary>Nombre del servicio al momento del pedido (el servicio puede cambiar de nombre después).</summary>
    public required string Descripcion { get; set; }

    public Unidad Unidad { get; set; }

    /// <summary>Kilos (puede ser decimal) o cantidad de piezas.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Precio por unidad al momento del pedido. No cambia si después se actualiza la lista.</summary>
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => decimal.Round(Cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);

    /// <summary>Para mostrar: "3,5 kg" o "2 pares".</summary>
    public string CantidadTexto
    {
        get
        {
            // Se formatea en es-AR a propósito: la app se usa en Argentina y el
            // servidor puede estar en un contenedor con cultura invariante, donde
            // el separador decimal es el punto.
            var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-AR");
            var numero = Cantidad.ToString("0.##", cultura);
            var unidad = Cantidad == 1m ? Unidades.Etiqueta(Unidad) : Unidades.EtiquetaPlural(Unidad);
            return $"{numero} {unidad}";
        }
    }
}
