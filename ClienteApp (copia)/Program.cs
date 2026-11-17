using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

const string UrlApi = "http://localhost:5180";

Console.WriteLine("=== APP CLIENTE (¡Tengo Hambre!) ===");

// La API está protegida con JWT: primero hay que iniciar sesión para recibir un token
Console.WriteLine("Iniciá sesión con tu usuario de la pizzería (el mismo que usás en la web).");
Console.Write("Usuario: ");
string usuario = Console.ReadLine() ?? "";
Console.Write("Contraseña: ");
string clave = LeerClaveOculta();

using HttpClient client = new HttpClient { BaseAddress = new Uri(UrlApi) };

try
{
    var cuerpoLogin = new StringContent(JsonSerializer.Serialize(new { usuario, clave }), Encoding.UTF8, "application/json");
    var respuestaLogin = await client.PostAsync("/api/auth/login", cuerpoLogin);

    if (!respuestaLogin.IsSuccessStatusCode)
    {
        Console.WriteLine($"No se pudo iniciar sesión (Código {(int)respuestaLogin.StatusCode}). Revisá usuario y contraseña.");
        return;
    }

    using var doc = JsonDocument.Parse(await respuestaLogin.Content.ReadAsStringAsync());
    string token = doc.RootElement.GetProperty("token").GetString() ?? "";

    // Desde acá, cada pedido a la API lleva el token en la cabecera Authorization
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    Console.WriteLine("Sesión iniciada. Token JWT recibido.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error de red: No se pudo contactar a la API. Detalle: ({ex.Message})");
    return;
}

Console.WriteLine("Presiona ENTER para pedir una Muzzarella...");
Console.ReadLine();

// Ajustado a strings planos para emparejar directamente con las columnas de tu base de datos física
var nuevoPedido = new {
    ClienteNombre = "Estudiante ET12",
    ClienteDireccion = "Av. Siempre Viva 123",
    PizzaVariedad = "Muzzarella Grande",
    PizzaPrecio = 8500.00m,
    Cantidad = 1
};

string json = JsonSerializer.Serialize(nuevoPedido);
var content = new StringContent(json, Encoding.UTF8, "application/json");

Console.WriteLine("Enviando pedido a la API por red...");

try
{
    var response = await client.PostAsync("/pedidos", content);

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

// Lee la contraseña sin mostrarla en pantalla
static string LeerClaveOculta()
{
    var sb = new StringBuilder();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0) sb.Length--;
            continue;
        }
        sb.Append(tecla.KeyChar);
    }
    return sb.ToString();
}
