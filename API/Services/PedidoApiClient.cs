using System.Net.Http.Json;
using PizzeriaBackend.Models;

namespace PizzeriaBackend.Services;

public class PedidoApiClient
{
    private readonly HttpClient _http;

    public PedidoApiClient(HttpClient http) => _http = http;

    public async Task<Pedido?> CrearPedidoAsync(PedidoRequestDTO dto)
    {
        var response = await _http.PostAsJsonAsync("api/pedidos", dto);

        if (!response.IsSuccessStatusCode)
        {
            var detalle = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(detalle);
        }

        return await response.Content.ReadFromJsonAsync<Pedido>();
    }
}