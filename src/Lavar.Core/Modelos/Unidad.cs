namespace Lavar.Core.Modelos;

/// <summary>Unidad en la que se cobra un servicio.</summary>
public enum Unidad
{
    /// <summary>Por kilo de ropa (el caso más común en una lavandería de barrio).</summary>
    Kilo,

    /// <summary>Por pieza: un colchón, una campera, un par de zapatillas.</summary>
    Unidad,

    /// <summary>Por par (zapatos).</summary>
    Par,

    /// <summary>Por juego completo (sábanas + fundas).</summary>
    Juego,
}

public static class Unidades
{
    /// <summary>Cómo se muestra en la UI y en los avisos: "kg", "unidad", "par", "juego".</summary>
    public static string Etiqueta(Unidad unidad) => unidad switch
    {
        Unidad.Kilo => "kg",
        Unidad.Unidad => "unidad",
        Unidad.Par => "par",
        Unidad.Juego => "juego",
        _ => "unidad",
    };

    /// <summary>El nombre en plural, para "3 kg" o "2 pares".</summary>
    public static string EtiquetaPlural(Unidad unidad) => unidad switch
    {
        Unidad.Kilo => "kg",
        Unidad.Unidad => "unidades",
        Unidad.Par => "pares",
        Unidad.Juego => "juegos",
        _ => "unidades",
    };
}
