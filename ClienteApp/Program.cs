using System.Text;
using System.Text.Json;

Console.WriteLine("=== APP CLIENTE (¡Tengo Hambre!) ===");
Console.WriteLine("Presiona ENTER para pedir una Muzzarella...");
Console.ReadLine();

using HttpClient client = new HttpClient();

// Ajustado a strings planos para emparejar directamente con las columnas de tu base de datos física
var nuevoPedido = new {
    ClienteNombre = "Estudiante ET12",
    ClienteDireccion = "Av. Corrientes 1234",
    PizzaVariedad = "Muzzarella con pepperoni",
    PizzaPrecio = 8500.00m,
    Cantidad = 1
};

string json = JsonSerializer.Serialize(nuevoPedido);
var content = new StringContent(json, Encoding.UTF8, "application/json");

Console.WriteLine("Enviando pedido a la API por red...");

try 
{
    var response = await client.PostAsync("http://localhost:5180/pedidos", content);
    
    if (response.IsSuccessStatusCode) 
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("\n¡PEDIDO RECIBIDO POR LA PIZZERÍA!");
        Console.WriteLine($"Ticket devuelto por el backend:\n{responseBody}");
    } 
    else 
    {
        Console.WriteLine($"El servidor rechazó el pedido (Código {response.StatusCode}).");
    }
} 
catch (Exception ex) 
{
    // Captura de errores asincrónicos si el backend está desconectado
    Console.WriteLine($"Error de red: No se pudo contactar a la API. Detalle: ({ex.Message})");
}


Console.WriteLine("\nPresiona ENTER para cerrar.");
Console.ReadLine();