using Lavar.Core.Modelos;

namespace Lavar.Api;

/// <summary>Lo que manda el panel para cargar un pedido.</summary>
public record ItemEntrada(Guid ServicioId, decimal Cantidad);

public record NuevoPedido(
    Guid? ClienteId,
    string? ClienteNombre,
    string? ClienteTelefono,
    bool RetiroADomicilio,
    string? Direccion,
    DateOnly? PrometidoPara,
    string? Notas,
    List<ItemEntrada>? Items);

public record CambioDeEstado(EstadoPedido Estado);

public record ClienteEntrada(string Nombre, string? Telefono, string? Email, string? Notas);

public record ServicioEntrada(string Nombre, Unidad Unidad, decimal Precio, bool? Activo, int? Orden);
