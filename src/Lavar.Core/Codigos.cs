using System.Security.Cryptography;
using System.Text;

namespace Lavar.Core;

/// <summary>
/// Códigos de retiro: lo único que el cliente tiene que recordar o mostrar.
///
/// Sin 0/O/1/I/L a propósito: el código se dice por teléfono y se copia de un
/// papel, y "0" contra "O" es la llamada de todos los días ("no, con cero...").
/// El alfabeto tiene 32 caracteres, así que con 6 posiciones hay ~1.000 millones
/// de combinaciones: adivinar el pedido de otro es inviable en un negocio chico.
/// </summary>
public static class Codigos
{
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Largo = 6;

    /// <summary>Genera un código nuevo que no esté en <paramref name="usados"/>.</summary>
    public static string Generar(ISet<string>? usados = null, int largo = Largo)
    {
        if (largo < 4) throw new ArgumentOutOfRangeException(nameof(largo), "El código tiene que tener al menos 4 caracteres.");

        for (var intento = 0; intento < 100; intento++)
        {
            var codigo = Nuevo(largo);
            if (usados == null || !usados.Contains(codigo)) return codigo;
        }

        throw new InvalidOperationException("No se pudo generar un código libre. Revisá el largo del código.");
    }

    /// <summary>
    /// Deja el código como lo escribe la gente: sin espacios ni guiones y en
    /// mayúsculas. Así "k7m 2qp" y "K7M-2QP" buscan el mismo pedido.
    /// </summary>
    public static string Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;

        var texto = new StringBuilder(codigo.Length);
        foreach (var caracter in codigo)
        {
            if (char.IsWhiteSpace(caracter) || caracter is '-' or '_') continue;
            texto.Append(char.ToUpperInvariant(caracter));
        }

        return texto.ToString();
    }

    private static string Nuevo(int largo)
    {
        var bytes = RandomNumberGenerator.GetBytes(largo);
        var codigo = new StringBuilder(largo);
        foreach (var b in bytes)
        {
            // Se descarta el resto para no favorecer a los primeros caracteres:
            // 256 no es múltiplo de 32... sí lo es (256 / 32 = 8), pero dejar el
            // cálculo explícito evita el bug si mañana cambia el alfabeto.
            var limite = 256 - (256 % Alfabeto.Length);
            var valor = b < limite ? b : (byte)(b % Alfabeto.Length);
            codigo.Append(Alfabeto[valor % Alfabeto.Length]);
        }

        return codigo.ToString();
    }
}
