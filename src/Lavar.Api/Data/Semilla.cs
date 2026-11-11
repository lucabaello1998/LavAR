using Lavar.Core.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Lavar.Api.Data;

/// <summary>
/// Carga inicial: la lista de servicios de una lavandería de barrio y, si se
/// pide, un pedido de demo para que el panel no arranque vacío.
/// </summary>
public static class Semilla
{
    public static readonly (string Nombre, Unidad Unidad, decimal Precio, int Orden)[] Servicios =
    [
        ("Lavado por kilo", Unidad.Kilo, 2500m, 1),
        ("Lavado + secado por kilo", Unidad.Kilo, 3200m, 2),
        ("Planchado por kilo", Unidad.Kilo, 2100m, 3),
        ("Acolchado 2 plazas", Unidad.Unidad, 18000m, 4),
        ("Acolchado 1 plaza", Unidad.Unidad, 14000m, 5),
        ("Almohada", Unidad.Unidad, 6000m, 6),
        ("Juego de sábanas", Unidad.Juego, 9000m, 7),
        ("Zapatillas", Unidad.Par, 8500m, 8),
        ("Tintorería (campera)", Unidad.Unidad, 12000m, 9),
    ];

    /// <summary>Crea los servicios que falten (idempotente: la app la llama en cada arranque).</summary>
    public static async Task ServiciosAsync(LavarDbContext db, CancellationToken ct = default)
    {
        var existentes = await db.Servicios.Select(s => s.Nombre).ToListAsync(ct);
        var faltantes = Servicios
            .Where(s => !existentes.Contains(s.Nombre))
            .Select(s => new Servicio
            {
                Nombre = s.Nombre,
                Unidad = s.Unidad,
                Precio = s.Precio,
                Orden = s.Orden,
            })
            .ToList();

        if (faltantes.Count == 0) return;

        db.Servicios.AddRange(faltantes);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Un cliente y dos pedidos de ejemplo, para ver el panel con datos.</summary>
    public static async Task DemoAsync(LavarDbContext db, string lavanderia, CancellationToken ct = default)
    {
        if (await db.Pedidos.AnyAsync(ct)) return;

        var servicios = await db.Servicios.OrderBy(s => s.Orden).ToListAsync(ct);
        if (servicios.Count == 0) return;

        var kilo = servicios.First(s => s.Unidad == Unidad.Kilo);
        var acolchado = servicios.First(s => s.Nombre.StartsWith("Acolchado 2"));
        var zapatillas = servicios.First(s => s.Unidad == Unidad.Par);

        var marta = new Cliente { Nombre = "Marta Giménez", Telefono = "11 5555 1234" };
        var julian = new Cliente { Nombre = "Julián Rossi", Telefono = "11 4444 9876" };

        var enCurso = new Pedido
        {
            Codigo = Core.Codigos.Generar(),
            Cliente = marta,
            RetiroADomicilio = true,
            Direccion = "Av. Rivadavia 5000, 3º B",
            PrometidoPara = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Notas = "Mancha de vino en el mantel: avisar si sale.",
        };
        enCurso.Agregar(kilo, 3.5m);
        enCurso.Agregar(acolchado, 1);
        enCurso.CambiarEstado(EstadoPedido.EnProceso);

        var listo = new Pedido
        {
            Codigo = Core.Codigos.Generar(),
            Cliente = julian,
            PrometidoPara = DateOnly.FromDateTime(DateTime.Today),
        };
        listo.Agregar(zapatillas, 1);
        listo.CambiarEstado(EstadoPedido.EnProceso);
        listo.CambiarEstado(EstadoPedido.Listo);

        db.Pedidos.AddRange(enCurso, listo);
        await db.SaveChangesAsync(ct);
    }
}
