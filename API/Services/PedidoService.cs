using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Data;
using PizzeriaBackend.Models;
using System.Net.Sockets;
using System.Text;

namespace PizzeriaBackend.Services;

public class PedidoService
{
    private readonly PizzeriaDb _db;

    public PedidoService(PizzeriaDb db) => _db = db;

    public Task<List<Pedido>> ObtenerActivosAsync() =>
        _db.Pedidos
           .Where(p => p.Activo)
           .Include(p => p.Cliente)
           .Include(p => p.Detalles).ThenInclude(d => d.Pizza)
           .ToListAsync();

    public async Task<Pedido> CrearAsync(PedidoRequestDTO dto)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == dto.ClienteNombre)
                      ?? new Cliente { Nombre = dto.ClienteNombre, Direccion = dto.ClienteDireccion };

        var detalles = new List<DetallePedido>();
        foreach (var item in dto.Items)
        {
            var pizza = await _db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == item.PizzaVariedad)
                        ?? throw new KeyNotFoundException(
                            $"La pizza '{item.PizzaVariedad}' ya no está disponible en el menú.");

            detalles.Add(new DetallePedido { Pizza = pizza, Cantidad = item.Cantidad });
        }

        var pedido = new Pedido
        {
            Cliente = cliente,
            ActorAsignado = "Cocina",
            Estado = "Espera de confirmación",
            Activo = true,
            Detalles = detalles,
            Momento = DateTime.Now
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();

        await NotificarCocinaAsync(pedido);
        return pedido;
    }

    private async Task NotificarCocinaAsync(Pedido pedido)
    {
        try
        {
            using var tcp = new TcpClient("127.0.0.1", 5050);
            using var stream = tcp.GetStream();
            var mensaje = Encoding.UTF8.GetBytes($"NUEVO_PEDIDO:{pedido.Id}");
            await stream.WriteAsync(mensaje);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[ALERTA] Cocina desconectada: {ex.Message}");
            pedido.Estado = "Error de red interno (Cocina no responde)";
            await _db.SaveChangesAsync();
        }
    }
}