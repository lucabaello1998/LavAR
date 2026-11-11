namespace Lavar.Core.Modelos;

/// <summary>Quien deja la ropa. Puede tener varios pedidos en curso a la vez.</summary>
public class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Nombre { get; set; }

    /// <summary>Teléfono como lo escribe la lavandería. Se normaliza para armar el link de WhatsApp.</summary>
    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public List<Pedido> Pedidos { get; set; } = new();
}
