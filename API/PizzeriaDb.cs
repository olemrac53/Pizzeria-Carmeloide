using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Models;

namespace PizzeriaBackend.Data;

public class PizzeriaDb : DbContext
{
    public PizzeriaDb(DbContextOptions<PizzeriaDb> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pizza> Pizzas => Set<Pizza>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
}