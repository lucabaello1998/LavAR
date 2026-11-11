using Lavar.Core.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Lavar.Api.Data;

public class LavarDbContext(DbContextOptions<LavarDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoItem> PedidoItems => Set<PedidoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(cliente =>
        {
            cliente.Property(c => c.Nombre).IsRequired().HasMaxLength(120);
            cliente.Property(c => c.Telefono).HasMaxLength(40);
            cliente.Property(c => c.Email).HasMaxLength(160);
            cliente.HasIndex(c => c.Nombre);
        });

        modelBuilder.Entity<Servicio>(servicio =>
        {
            servicio.Property(s => s.Nombre).IsRequired().HasMaxLength(120);
            // SQLite no tiene decimal nativo: se guarda como REAL y se redondea al mostrar.
            servicio.Property(s => s.Precio).HasColumnType("TEXT");
            servicio.HasIndex(s => s.Nombre).IsUnique();
        });

        modelBuilder.Entity<Pedido>(pedido =>
        {
            // El código es la llave que usa el cliente: no puede repetirse.
            pedido.Property(p => p.Codigo).IsRequired().HasMaxLength(12);
            pedido.HasIndex(p => p.Codigo).IsUnique();
            pedido.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
            pedido.HasOne(p => p.Cliente)
                .WithMany(c => c.Pedidos)
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            pedido.HasMany(p => p.Items)
                .WithOne()
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
            pedido.Ignore(p => p.Total);
        });

        modelBuilder.Entity<PedidoItem>(item =>
        {
            item.Property(i => i.Descripcion).IsRequired().HasMaxLength(120);
            item.Property(i => i.Unidad).HasConversion<string>().HasMaxLength(12);
            item.Property(i => i.PrecioUnitario).HasColumnType("TEXT");
            item.Property(i => i.Cantidad).HasColumnType("TEXT");
            item.Ignore(i => i.Subtotal);
            item.Ignore(i => i.CantidadTexto);
            item.HasOne(i => i.Servicio)
                .WithMany()
                .HasForeignKey(i => i.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
