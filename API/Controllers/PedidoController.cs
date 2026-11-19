using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace PizzeriaBackend.Controllers
{
    // ==========================================
    // [COOKIES] 5. RESTRICCIÓN Y VALIDACIÓN DE ACCESO
    // ==========================================
    // El atributo [Authorize] le indica a ASP.NET Core que este controlador completo 
    // está protegido. Cuando el usuario intenta entrar aquí, el sistema verifica que 
    // la cookie "PizzeriaAuth" esté presente y sea válida. Si no existe o expiró, 
    // el usuario es redirigido automáticamente a la página de Login configurada en Program.cs.
    [Authorize]
    public class PedidoController : Controller
    {
        private readonly PizzeriaDb _db;

        public PedidoController(PizzeriaDb db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Agregar(int? pizzaId = null)
        {
            var pizzas = await _db.Pizzas
                .OrderBy(p => p.Id)
                .ToListAsync();

            ViewBag.Pizzas = pizzas;
            ViewBag.PizzaIdSeleccionada = pizzaId;

            return View(new PedidoFormViewModel());
        }

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
                ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Id).ToListAsync();
                return View(model);
            }

            try
            {
                var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == model.ClienteNombre)
                              ?? new Cliente { Nombre = model.ClienteNombre, Direccion = model.ClienteDireccion };

                var detalles = new List<DetallePedido>();

                foreach (var item in model.Items)
                {
                    var pizza = await _db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == item.PizzaVariedad);
                    if (pizza == null)
                    {
                        ModelState.AddModelError(string.Empty,
                            $"La pizza '{item.PizzaVariedad}' ya no está disponible en el menú.");
                        ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Id).ToListAsync();
                        return View(model);
                    }

                    detalles.Add(new DetallePedido { Pizza = pizza, Cantidad = item.Cantidad });
                }

                var nuevoPedido = new Pedido
                {
                    Cliente = cliente,
                    ActorAsignado = "Cocina",
                    Estado = "Espera de confirmación",
                    Activo = true,
                    Detalles = detalles
                };

                _db.Pedidos.Add(nuevoPedido);
                await _db.SaveChangesAsync();

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
                    ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Id).ToListAsync();
                }
                catch
                {
                    ViewBag.Pizzas = new List<Pizza>();
                }

                return View(model);
            }
        }

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