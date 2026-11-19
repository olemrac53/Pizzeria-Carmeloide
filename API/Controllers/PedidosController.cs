using Microsoft.AspNetCore.Mvc;
using PizzeriaBackend.Models;
using PizzeriaBackend.Services;

namespace PizzeriaBackend.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly PedidoService _service;

    public PedidosController(PedidoService service) => _service = service;

    // GET api/pedidos
    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _service.ObtenerActivosAsync());

    // POST api/pedidos
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PedidoRequestDTO dto)
    {
        if (dto.Items.Count == 0)
            return BadRequest("Agregá al menos una pizza al pedido.");

        try
        {
            var pedido = await _service.CrearAsync(dto);
            return Created($"/api/pedidos/{pedido.Id}", pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}