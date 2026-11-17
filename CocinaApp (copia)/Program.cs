using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

const string UrlApi = "http://localhost:5180";

Console.WriteLine("=== MÓDULO INTERNO: COCINA ===");

// La API está protegida con JWT: la cocina inicia sesión una vez y usa el token
// para avisarle a la API cada cambio de estado.
Console.Write("Usuario de la cocina: ");
string usuario = Console.ReadLine() ?? "";
Console.Write("Contraseña: ");
string clave = LeerClaveOculta();

using HttpClient httpClient = new HttpClient { BaseAddress = new Uri(UrlApi) };

// Pide un token a la API y lo deja guardado en las cabeceras del HttpClient
async Task<bool> IniciarSesionAsync()
{
    try
    {
        var cuerpo = new StringContent(JsonSerializer.Serialize(new { usuario, clave }), Encoding.UTF8, "application/json");
        var respuesta = await httpClient.PostAsync("/api/auth/login", cuerpo);
        if (!respuesta.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        string token = doc.RootElement.GetProperty("token").GetString() ?? "";
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] No se pudo contactar a la API para iniciar sesión: {ex.Message}");
        return false;
    }
}

if (!await IniciarSesionAsync())
{
    Console.WriteLine("No se pudo iniciar sesión en la API. Revisá usuario y contraseña, y que la API esté encendida.");
    return;
}
Console.WriteLine("Cocina autenticada en la API (token JWT recibido).");

// El puerto 5050 evitará el conflicto de 'Address already in use'
TcpListener server = new TcpListener(System.Net.IPAddress.Parse("127.0.0.1"), 5050);
server.Start();
Console.WriteLine("Cocina en línea. Esperando comandas por Socket en puerto 5050...");

while (true)
{
    // Acepta la conexión entrante de la API de forma asincrónica
    using TcpClient client = await server.AcceptTcpClientAsync();
    using NetworkStream stream = client.GetStream();

    byte[] buffer = new byte[1024];
    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
    string mensaje = Encoding.UTF8.GetString(buffer, 0, bytesRead);

    Console.WriteLine($"\n[SOCKET] Señal recibida: {mensaje}");

    if (mensaje.StartsWith("NUEVO_PEDIDO"))
    {
        string id = mensaje.Split(':')[1];
        Console.WriteLine($"[COCINA] Empezando a amasar el pedido #{id}...");

        // Simula el tiempo de cocción asincrónico exigido por el plan (5 segundos)
        await Task.Delay(5000);

        try
        {
            // La cocina llama a la API para cambiar el estado a "En preparación"
            string ruta = $"/pedidos/{id}/estado?nuevoEstado=En%20preparacion";
            var response = await httpClient.PutAsync(ruta, null);

            // Si el token venció (la cocina puede quedar horas encendida), pide uno nuevo y reintenta
            if (response.StatusCode == HttpStatusCode.Unauthorized && await IniciarSesionAsync())
            {
                response = await httpClient.PutAsync(ruta, null);
            }

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[COCINA] Api notificada: Pedido #{id} actualizado a 'En preparación'.");
            }
            else
            {
                Console.WriteLine($"[COCINA] La API rechazó el cambio de estado (Código {(int)response.StatusCode}).");
            }
        }
        catch (Exception ex)
        {
            // Control de excepciones en entornos distribuidos
            Console.WriteLine($"[ERROR] La pizza está lista pero la API se cayó o no responde: {ex.Message}");
        }
    }
}

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
