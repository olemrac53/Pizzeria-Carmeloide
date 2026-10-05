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
            // ordenadas por nombre, para llenar los <select> del formulario.
            var pizzas = await _db.Pizzas
                .OrderBy(p => p.Variedad)
                .ToListAsync();

            ViewBag.Pizzas = pizzas;

            return View(new PedidoFormViewModel());
        }

        // POST: /Pedido/Agregar (Procesa el formulario HTML y dispara sockets)
        // Ahora recibe una LISTA de pizzas (model.Items), no una sola.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(PedidoFormViewModel model)
        {
            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "Agregá al menos una pizza al pedido.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Variedad).ToListAsync();
                return View(model);
            }

            try
            {
                // Busca o crea el cliente
                var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == model.ClienteNombre)
                              ?? new Cliente { Nombre = model.ClienteNombre, Direccion = model.ClienteDireccion };

                // Arma un DetallePedido por cada variedad elegida. El precio SIEMPRE
                // se toma de la base de datos (nunca del navegador), para que nadie
                // pueda mandar un precio manipulado desde el HTML.
                var detalles = new List<DetallePedido>();

                foreach (var item in model.Items)
                {
                    var pizza = await _db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == item.PizzaVariedad);
                    if (pizza == null)
                    {
                        ModelState.AddModelError(string.Empty,
                            $"La pizza '{item.PizzaVariedad}' ya no está disponible en el menú.");
                        ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Variedad).ToListAsync();
                        return View(model);
                    }

                    detalles.Add(new DetallePedido { Pizza = pizza, Cantidad = item.Cantidad });
                }

                // Construcción de la entidad relacional con sus Foreign Keys
                var nuevoPedido = new Pedido
                {
                    Cliente = cliente,
                    ActorAsignado = "Cocina",
                    Estado = "Espera de confirmación",
                    Activo = true,
                    Detalles = detalles
                };

                // Guardado atómico en la base de datos MySQL
                _db.Pedidos.Add(nuevoPedido);
                await _db.SaveChangesAsync();

                Console.WriteLine($"[WEB-MVC] Pedido #{nuevoPedido.Id} creado desde HTML con {detalles.Count} tipo(s) de pizza. Enviando socket a Cocina...");

                // Disparo por Socket TCP hacia CocinaApp (puerto 5050)
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

                // Redirecciona a la vista de confirmación pasando el ID generado
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

                return View(model);
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