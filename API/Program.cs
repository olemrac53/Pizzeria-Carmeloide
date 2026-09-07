using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // CONFIGURACIÓN DE LA BASE DE DATOS (Abstracción)
        var connectionString = "Server=localhost;Database=5to_PizzeriaDB;User=5to_agbd;Password=Trigg3rs!;";

        // Inyectamos el DbContext usando Pomelo para interactuar de forma abstracta en C#
        builder.Services.AddDbContext<PizzeriaDb>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger(options => { options.RouteTemplate = "/openapi/{documentName}.json"; });
            app.MapScalarApiReference();
        }

        app.MapGet("/pedidos", async (PizzeriaDb db) =>
            await db.Pedidos.Where(p => p.Activo).ToListAsync()
        );

        // POST: Crear un nuevo pedido e integrar comunicación por Sockets
        app.MapPost("/pedidos", async (PedidoDTO dto, PizzeriaDb db) =>
        {
            try
            {
                // 1. Busca si el cliente ya existe en la BD. Si no, crea uno nuevo.
                var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Nombre == dto.ClienteNombre)
                              ?? new Cliente { Nombre = dto.ClienteNombre, Direccion = dto.ClienteDireccion };

                // 2. Busca si la pizza existe en el menú. Si no, la crea.
                var pizza = await db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == dto.PizzaVariedad)
                            ?? new Pizza { Variedad = dto.PizzaVariedad, Precio = dto.PizzaPrecio };

                // 3. Arma el pedido relacional conectando las tablas
                var nuevo = new Pedido
                {
                    Cliente = cliente,
                    ActorAsignado = "Cocina",
                    Estado = "Espera de confirmación",
                    Activo = true,
                    Detalles = new List<DetallePedido> {
                new DetallePedido { Pizza = pizza, Cantidad = dto.Cantidad }
                    }
                };

                // El ORM guarda todo automáticamente en las 4 tablas con sus Claves Foráneas
                db.Pedidos.Add(nuevo);
                await db.SaveChangesAsync();

                Console.WriteLine($"[API] Pedido #{nuevo.Id} registrado en BD. Notificando a cocina...");

                // Enviar Socket a la cocina
                try
                {
                    using var tcpClient = new TcpClient("128.0.0.1", 5050);
                    using var stream = tcpClient.GetStream();
                    var mensaje = Encoding.UTF8.GetBytes($"NUEVO_PEDIDO:{nuevo.Id}");
                    await stream.WriteAsync(mensaje);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[ALERTA] Cocina desconectada: {ex.Message}");
                    nuevo.Estado = "Error de red interno (Cocina no responde)";
                    await db.SaveChangesAsync();
                }

                return Results.Created($"/pedidos/{nuevo.Id}", nuevo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR FATAL] {ex.Message}");
                return Results.Problem("Error interno del servidor.");
            }
        });

        // PUT: Actualizar estado del pedido (Llamado de forma distribuida por la Cocina)
        app.MapPut("/pedidos/{id}/estado", async (int id, string nuevoEstado, PizzeriaDb db) =>
        {
            var pedido = await db.Pedidos.FindAsync(id);
            if (pedido is null) return Results.NotFound();

            pedido.Estado = nuevoEstado;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // DELETE: Borrado lógico interactuando únicamente con el objeto de C#
        app.MapDelete("/pedidos/{id}", async (int id, PizzeriaDb db) =>
        {
            if (await db.Pedidos.FindAsync(id) is Pedido pedido)
            {
                pedido.Activo = false;
                await db.SaveChangesAsync();
                return Results.NoContent();
            }

            return Results.NotFound();
        });

        app.Run();
    }
}