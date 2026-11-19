using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. INYECCIÓN DEL SERVICIO MVC
builder.Services.AddControllersWithViews();

// ======================================================================
// [COOKIES] 1. CONFIGURACIÓN DEL SERVICIO DE AUTENTICACIÓN
// ======================================================================
// Aquí se registra el esquema de autenticación por cookies. 
// - Cookie.Name: El nombre que tendrá la cookie en el navegador del cliente ("PizzeriaAuth").
// - HttpOnly: Impide que scripts de JavaScript accedan a la cookie (protección vital contra ataques XSS).
// - SameSite: Define cómo se comparte la cookie con sitios de terceros (Lax protege contra ataques CSRF).
// - LoginPath: La ruta a la que se redirige automáticamente al usuario si intenta acceder a una sección protegida sin estar logueado.
// - ExpireTimeSpan: El tiempo que dura la sesión activa antes de expirar.
// - SlidingExpiration: Si está en true, renueva el tiempo de vida de la cookie cada vez que el usuario interactúa con la web.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PizzeriaAuth";
        options.Cookie.HttpOnly = true;                  
        options.Cookie.SameSite = SameSiteMode.Lax;      
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; 
        options.LoginPath = "/Account/Login";            
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);  
        options.SlidingExpiration = true;                
    });

// CONFIGURACIÓN DE LA BASE DE DATOS (MySQL)
var connectionString = "Server=localhost;Database=5to_PizzeriaDB;User=root;Password=root;";

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

// Habilita los archivos estáticos de la carpeta wwwroot (CSS, imágenes, JS)
app.UseStaticFiles(); 
app.UseRouting();     

// ======================================================================
// [COOKIES] 2. MIDDLEWARES DE AUTENTICACIÓN Y AUTORIZACIÓN
// ======================================================================
// IMPORTANTE: El orden de estas líneas es estricto y obligatorio.
// - UseAuthentication(): Lee la cookie "PizzeriaAuth" que envía el navegador, la descifra y reconstruye la identidad del usuario (User).
// - UseAuthorization(): Revisa si el usuario actual tiene permisos para entrar a las rutas protegidas con [Authorize].
app.UseAuthentication(); 
app.UseAuthorization(); 

// ----------------------------------------------------------------------
// ENDPOINTS DE API MINIMAL Y SOCKETS (Comunicación con Cocina)
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

// 3. MAPEO DE RUTAS PARA LOS CONTROLADORES MVC (Home, Account, Pedido, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();