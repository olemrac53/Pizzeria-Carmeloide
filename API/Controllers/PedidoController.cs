using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using PizzeriaBackend.Services;
using Microsoft.AspNetCore.Authorization;

namespace PizzeriaBackend.Controllers
{
    [Authorize]
    public class PedidoController : Controller
    {
        private readonly PizzeriaDb _db;
        private readonly PedidoApiClient _api;

        public PedidoController(PizzeriaDb db, PedidoApiClient api)
        {
            _db = db;
            _api = api;
        }

        [HttpGet]
        public async Task<IActionResult> Agregar(int? pizzaId = null)
        {
            await CargarPizzas();
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
                await CargarPizzas();
                return View(model);
            }

            var dto = new PedidoRequestDTO
            {
                ClienteNombre = model.ClienteNombre,
                ClienteDireccion = model.ClienteDireccion,
                UsuarioId = ObtenerUsuarioId(),
                Items = model.Items.Select(i => new ItemPedidoDTO
                {
                    PizzaVariedad = i.PizzaVariedad,
                    Cantidad = i.Cantidad
                }).ToList()
            };

            try
            {
                var pedido = await _api.CrearPedidoAsync(dto);
                return RedirectToAction(nameof(Confirmacion), new { id = pedido!.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo contactar a la API de pedidos.");
            }

            await CargarPizzas();
            return View(model);
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

        [HttpGet]
        public async Task<IActionResult> MisPedidos()
        {
            var uid = ObtenerUsuarioId();
            if (uid == null)
            {
                return Forbid();
            }

            var pedidos = await _db.Pedidos
                .Where(p => p.UsuarioId == uid)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Pizza)
                .OrderByDescending(p => p.Momento)
                .ToListAsync();

            return View(pedidos);
        }

        private int? ObtenerUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out var id) ? id : null;
        }

        private async Task CargarPizzas()
        {
            ViewBag.Pizzas = await _db.Pizzas.OrderBy(p => p.Id).ToListAsync();
        }
    }
}