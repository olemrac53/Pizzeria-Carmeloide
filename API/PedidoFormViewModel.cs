namespace PizzeriaBackend.Models;

// Este ViewModel es específico del formulario Views/Pedido/Agregar.cshtml.
// A diferencia de PedidoDTO (que usa la API minimal en Program.cs y solo
// soporta UNA variedad de pizza), este permite varias filas de pizza en
// un mismo pedido mediante la lista Items.
public class PedidoFormViewModel
{
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDireccion { get; set; } = string.Empty;
    public List<ItemPedidoViewModel> Items { get; set; } = new();
}

public class ItemPedidoViewModel
{
    public string PizzaVariedad { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}