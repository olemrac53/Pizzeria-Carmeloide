using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies; // <-- IMPORTANTE: Namespace para cookies

var builder = WebApplication.CreateBuilder(args);

// 1. INYECCIÓN DEL SERVICIO MVC
builder.Services.AddControllersWithViews();

// CONFIGURACIÓN DE COOKIES DE AUTENTICACIÓN
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Redirige aquí si el usuario no está logueado
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Tiempo de expiración de la sesión
    });

// CONFIGURACIÓN DE LA BASE DE DATOS (Abstracción)
var connectionString = "Server=localhost;Database=5to_PizzeriaDB;User=5to_agbd;Password=Trigg3rs!;";

builder.Services.AddDbContext<PizzeriaDb>(options => 
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => { options.RouteTemplate = "/openapi/{documentName}.json"; });
    app.MapScalarApiReference();
}

// 2. MIDDLEWARES NECESARIOS PARA MVC Y SEGURIDAD
app.UseStaticFiles(); 
app.UseRouting();     

// ORDEN CRUCIAL: Authentication debe ir antes de Authorization
app.UseAuthentication(); 
app.UseAuthorization(); 

// ----------------------------------------------------------------------
// TUS ENDPOINTS DE API MINIMAL Y SOCKETS 
// ----------------------------------------------------------------------
app.MapGet("/pedidos", async (PizzeriaDb db) => 
    await db.Pedidos.Where(p => p.Activo).ToListAsync()
);

app.MapPost("/pedidos", async (PedidoDTO dto, PizzeriaDb db) => 
{
    try 
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Nombre == dto.ClienteNombre)
                      ?? new Cliente { Nombre = dto.ClienteNombre, Direccion = dto.ClienteDireccion };

        var pizza = await db.Pizzas.FirstOrDefaultAsync(p => p.Variedad == dto.PizzaVariedad)
                    ?? new Pizza { Variedad = dto.PizzaVariedad, Precio = dto.PizzaPrecio };

        var nuevo = new Pedido
        {
            Cliente = cliente,
            ActorAsignado = "Cocina",
            Estado = "Espera de confirmación",
            Activo = true,
            Detalles = new List<DetallePedido> {
                new DetallePedido { Pizza = pizza, Cantidad = dto.Cantidad }
            }
        };
        
        db.Pedidos.Add(nuevo);
        await db.SaveChangesAsync();

        Console.WriteLine($"[API] Pedido #{nuevo.Id} registrado en BD. Notificando a cocina...");

        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient("127.0.0.1", 5050);
            using var stream = tcpClient.GetStream();
            var mensaje = System.Text.Encoding.UTF8.GetBytes($"NUEVO_PEDIDO:{nuevo.Id}");
            await stream.WriteAsync(mensaje);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Console.WriteLine($"[ALERTA] Cocina desconectada: {ex.Message}");
            nuevo.Estado = "Error de red interno (Cocina no responde)";
            await db.SaveChangesAsync();
        }

        return Results.Created($"/pedidos/{nuevo.Id}", nuevo);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR FATAL] {ex.Message}");
        return Results.Problem("Error interno del servidor.");
    }
});

app.MapPut("/pedidos/{id}/estado", async (int id, string nuevoEstado, PizzeriaDb db) => 
{
    var pedido = await db.Pedidos.FindAsync(id);
    if (pedido is null) return Results.NotFound();

    pedido.Estado = nuevoEstado;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapDelete("/pedidos/{id}", async (int id, PizzeriaDb db) => 
{
    if (await db.Pedidos.FindAsync(id) is Pedido pedido)
    {
        pedido.Activo = false; 
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    return Results.NotFound();
});

// 3. MAPEO DE RUTAS PARA LOS CONTROLADORES MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

//corregis que el cliente no pueda escribir la variedad de la pizza, hacer un landing lindo 
//crear usuario
//iniciar sesion usuario