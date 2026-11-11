using System.Globalization;
using System.Text;
using Lavar.Core.Modelos;

namespace Lavar.Core;

/// <summary>
/// Los avisos al cliente: mensaje ya escrito y link de WhatsApp.
///
/// No se manda nada solo. La app arma el link y la lavandería aprieta "enviar"
/// en su propio WhatsApp: no hace falta ninguna API paga, ni dar de alta un
/// número de WhatsApp Business, ni que el cliente instale nada. El mismo texto
/// se copia y pega en un SMS si el cliente no tiene WhatsApp.
/// </summary>
public static class Avisos
{
    /// <summary>Código de país por defecto (Argentina).</summary>
    public const string PaisPorDefecto = "54";

    /// <summary>
    /// Deja el teléfono en el formato que pide wa.me: solo dígitos, con el país
    /// adelante, el 9 de celular y sin el 0 ni el 15 de las formas locales.
    ///
    /// <code>
    /// "011 15-5555-1234"  → "5491155551234"
    /// "+54 9 11 5555 1234" → "5491155551234"
    /// "11 5555 1234"       → "5491155551234"
    /// "5555-1234"          → null  (no alcanza para saber de dónde es)
    /// </code>
    /// </summary>
    public static string? NormalizarTelefono(string? crudo)
    {
        if (string.IsNullOrWhiteSpace(crudo)) return null;

        var digitos = new string(crudo.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0) return null;

        // Ya viene con el país: 54 + (9) + área + número.
        if (digitos.StartsWith(PaisPorDefecto, StringComparison.Ordinal) && digitos.Length > 10)
            return PaisPorDefecto + NormalizarParteLocal(digitos[PaisPorDefecto.Length..]);

        // Forma local: con 0 inicial y/o con el 15 de celular.
        if (digitos.StartsWith('0')) digitos = digitos[1..];

        // 0 + área(2) + 15 + número(8): "11 15 5555 1234" → "111555551234".
        if (digitos.Length == 12 && digitos.Substring(2, 2) == "15")
            return PaisPorDefecto + "9" + digitos[..2] + digitos[4..];

        // 0 + área(3) + 15 + número(7): "341 15 555 1234" → "341155551234".
        if (digitos.Length == 12 && digitos.Substring(3, 2) == "15")
            return PaisPorDefecto + "9" + digitos[..3] + digitos[5..];

        var parte = NormalizarParteLocal(digitos);
        return parte.Length == 0 ? null : PaisPorDefecto + parte;
    }

    /// <summary>
    /// El número sin país, con el 9 de celular adelante cuando corresponde, o
    /// cadena vacía si no se puede interpretar.
    /// </summary>
    private static string NormalizarParteLocal(string digitos)
    {
        if (digitos.StartsWith('0')) digitos = digitos[1..];

        // 10 dígitos = área (2 o 3) + número. Se asume celular y se le pone el 9
        // adelante, que es lo que diferencia un celular de un fijo en Argentina.
        if (digitos.Length == 10)
            return digitos.StartsWith('9') ? digitos : "9" + digitos;

        // 11 dígitos que ya empiezan con 9: 9 + área (3) + número (7).
        if (digitos.Length == 11 && digitos.StartsWith('9'))
            return digitos;

        // Cualquier otro largo no se puede armar con seguridad: mejor no inventar
        // un número y que la UI ofrezca copiar el mensaje.
        return string.Empty;
    }

    /// <summary>El texto que se manda. Se arma con el código y el estado, sin datos de más.</summary>
    public static string MensajePedido(Pedido pedido, string nombreLavanderia = "la lavandería")
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var nombre = string.IsNullOrWhiteSpace(pedido.Cliente?.Nombre)
            ? string.Empty
            : $"{pedido.Cliente!.Nombre.Split(' ')[0]}, ";

        var mensaje = new StringBuilder()
            .Append(nombre)
            .Append("tu pedido ")
            .Append(pedido.Codigo)
            .Append(' ')
            .Append(Estados.ParaElCliente(pedido.Estado))
            .Append('.');

        if (pedido.Estado is EstadoPedido.EnProceso or EstadoPedido.Listo && pedido.PrometidoPara is { } fecha)
            mensaje.Append(" Lo tenés listo el ").Append(fecha.ToString("dd/MM", CultureInfo.InvariantCulture)).Append('.');

        if (pedido.Estado == EstadoPedido.Listo && pedido.RetiroADomicilio)
            mensaje.Append(" Te lo llevamos a domicilio.");

        mensaje.Append(" — ").Append(nombreLavanderia);

        return mensaje.ToString();
    }

    /// <summary>
    /// El link para abrir WhatsApp con el mensaje ya escrito, o null si el
    /// teléfono no se puede normalizar (ahí la UI ofrece copiar el texto).
    /// </summary>
    public static string? LinkWhatsApp(string? telefono, string mensaje)
    {
        var numero = NormalizarTelefono(telefono);
        if (numero is null) return null;

        return $"https://wa.me/{numero}?text={Uri.EscapeDataString(mensaje)}";
    }
}
