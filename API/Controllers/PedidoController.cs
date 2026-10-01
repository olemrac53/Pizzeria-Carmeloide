using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;

namespace PizzeriaBackend.Controllers
{
    public class PedidoController : Controller
    {
        private readonly PizzeriaDb _db;

        public PedidoController(PizzeriaDb db)
        {
            _db = db;
        }

        // GET: /Pedido/Agregar (Muestra el formulario HTML)
        [HttpGet]
        public async Task<IActionResult> Agregar()
        {
            // Trae las pizzas reales cargadas en la tabla Pizzas de la base de datos,
            // ordenadas por nombre, para llenar el <select> del formulario.
            var pizzas = await _db.Pizzas
                .OrderBy(p => p.Variedad)
                .ToListAsync();

            ViewBag.Pizzas = pizzas;

            return View();
        }

        // POST: /Pedido/Agregar (Procesa el formulario HTML y dispara sockets)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(PedidoDTO dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Variedad).ToListAsync();
                return View(dto);
            }

            try
            {
                var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == dto.ClienteNombre)
                              ?? new Cliente { Nombre = dto.ClienteNombre, Direccion = dto.ClienteDireccion };

                var pizza = await _db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == dto.PizzaVariedad)
                            ?? new Pizza { Variedad = dto.PizzaVariedad, Precio = dto.PizzaPrecio };

                var nuevoPedido = new Pedido
                {
                    Cliente = cliente,
                    ActorAsignado = "Cocina",
                    Estado = "Espera de confirmación",
                    Activo = true,
                    Detalles = new List<DetallePedido>
                    {
                        new DetallePedido { Pizza = pizza, Cantidad = dto.Cantidad }
                    }
                };

                _db.Pedidos.Add(nuevoPedido);
                await _db.SaveChangesAsync();

                Console.WriteLine($"[WEB-MVC] Pedido #{nuevoPedido.Id} creado desde HTML. Enviando socket a Cocina...");

                try
                {
                    using var tcpClient = new TcpClient("127.0.0.1", 5050);
                    using var stream = tcpClient.GetStream();
                    byte[] mensaje = Encoding.UTF8.GetBytes($"NUEVO_PEDIDO:{nuevoPedido.Id}");
                    await stream.WriteAsync(mensaje, 0, mensaje.Length);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[ALERTA] Cocina no conectada en puerto 5050: {ex.Message}");
                    nuevoPedido.Estado = "Error de red interno (Cocina no responde)";
                    await _db.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Confirmacion), new { id = nuevoPedido.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR EN PEDIDO] {ex.Message}");
                ModelState.AddModelError(string.Empty, "Ocurrió un fallo registrando el pedido en la base de datos.");

                try
                {
                    ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Variedad).ToListAsync();
                }
                catch
                {
                    ViewBag.Pizzas = new List<Pizza>();
                }

                return View(dto);
            }
        }

        // GET: /Pedido/Confirmacion/5 (Comprobante y seguimiento de estado)
        [HttpGet]
        public async Task<IActionResult> Confirmacion(int id)
        {
            var pedido = await _db.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Pizza)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            return View(pedido);
        }
    }
}