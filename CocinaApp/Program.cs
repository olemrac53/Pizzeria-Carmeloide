using System.Net.Sockets;
using System.Text;

Console.WriteLine("=== MÓDULO INTERNO: COCINA ===");

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
        
        using HttpClient httpClient = new HttpClient();
        try 
        {
            // La cocina llama a la API para cambiar el estado a "En preparación"
            var response = await httpClient.PutAsync($"http://localhost:5180/pedidos/{id}/estado?nuevoEstado=En%20preparacion", null);
            if (response.IsSuccessStatusCode) 
            {
                Console.WriteLine($"[COCINA] Api notificada: Pedido #{id} actualizado a 'En preparación'.");
            }
        } 
        catch (Exception ex)
        {
            // Control de excepciones en entornos distribuidos
            Console.WriteLine($"[ERROR] La pizza está lista pero la API se cayó o no responde: {ex.Message}");
        }
    }
}