namespace PizzeriaBackend.Models;

public class PedidoRequestDTO
{
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDireccion { get; set; } = string.Empty;
    public List<ItemPedidoDTO> Items { get; set; } = new();
}

public class ItemPedidoDTO
{
    public string PizzaVariedad { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}