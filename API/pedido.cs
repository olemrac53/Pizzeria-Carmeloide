namespace PizzeriaBackend.Models;

public class Pedido
{
    public int Id { get; set; }
    
    // Clave Foránea y relación hacia el Cliente
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    
    public string? ActorAsignado { get; set; }
    public string? Estado { get; set; }
    public bool Activo { get; set; }
    public DateTime Momento { get; set; }
    
    // Relación hacia los detalles del pedido
    public List<DetallePedido> Detalles { get; set; } = new();
}