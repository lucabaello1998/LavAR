namespace Lavar.Core.Modelos;

/// <summary>
/// El pedido: una o varias prendas o kilos que el cliente dejó (o pidió que
/// pasen a buscar) y que la lavandería va moviendo de estado.
///
/// El <see cref="Codigo"/> es lo único que el cliente necesita para consultar:
/// nadie se registra, no hay contraseña que se olvide.
/// </summary>
public class Pedido
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código corto que se le dice al cliente ("K7M2QP").</summary>
    public required string Codigo { get; set; }

    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public EstadoPedido Estado { get; set; } = EstadoPedido.Recibido;

    public DateTime RecibidoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha en que la lavandería prometió tenerlo listo.</summary>
    public DateOnly? PrometidoPara { get; set; }

    public DateTime? EntregadoEn { get; set; }

    /// <summary>Si la lavandería pasa a buscar la ropa por el domicilio del cliente.</summary>
    public bool RetiroADomicilio { get; set; }

    public string? Direccion { get; set; }

    /// <summary>Anotaciones del mostrador: "mancha de vino en el mantel", "no usar lavandina".</summary>
    public string? Notas { get; set; }

    public List<PedidoItem> Items { get; set; } = new();

    public decimal Total => Items.Sum(item => item.Subtotal);

    /// <summary>
    /// Agrega un servicio al pedido copiando el precio de la lista. Copiar el
    /// precio no es un detalle: si mañana sube el kilo, este pedido tiene que
    /// seguir costando lo que se acordó hoy.
    /// </summary>
    public PedidoItem Agregar(Servicio servicio, decimal cantidad)
    {
        ArgumentNullException.ThrowIfNull(servicio);

        if (cantidad <= 0)
            throw new ReglaDeNegocioException("La cantidad tiene que ser mayor que cero.");

        if (servicio.Unidad is Unidad.Par or Unidad.Juego && cantidad != decimal.Truncate(cantidad))
            throw new ReglaDeNegocioException(
                $"\"{servicio.Nombre}\" se cobra por {Unidades.Etiqueta(servicio.Unidad)}: la cantidad tiene que ser un número entero.");

        var item = new PedidoItem
        {
            PedidoId = Id,
            ServicioId = servicio.Id,
            Descripcion = servicio.Nombre,
            Unidad = servicio.Unidad,
            Cantidad = cantidad,
            PrecioUnitario = servicio.Precio,
        };

        Items.Add(item);
        return item;
    }

    /// <summary>
    /// Mueve el pedido de estado aplicando las reglas de <see cref="Estados"/>.
    /// Si el cambio no está permitido, no lo hace y avisa por qué.
    /// </summary>
    public void CambiarEstado(EstadoPedido nuevo)
    {
        if (!Estados.PuedePasar(Estado, nuevo))
        {
            throw new ReglaDeNegocioException(nuevo == EstadoPedido.Entregado
                ? "Solo se puede entregar un pedido que está listo."
                : $"No se puede pasar de \"{Estados.Etiqueta(Estado)}\" a \"{Estados.Etiqueta(nuevo)}\".");
        }

        Estado = nuevo;

        if (nuevo == EstadoPedido.Entregado)
            EntregadoEn = DateTime.UtcNow;
        else
            EntregadoEn = null;
    }
}

/// <summary>Un dato que no cumple una regla del negocio (no es un error técnico).</summary>
public class ReglaDeNegocioException(string mensaje) : Exception(mensaje);
