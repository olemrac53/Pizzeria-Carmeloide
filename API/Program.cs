using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using PizzeriaBackend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. INYECCIÓN DEL SERVICIO MVC
builder.Services.AddControllersWithViews();


// Aca se registra el esquema de autenticación por cookies. 
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
var connectionString = "Server=localhost;Database=5to_PizzeriaDB;User=5to_agbd;Password=Trigg3rs!;";

builder.Services.AddDbContext<PizzeriaDb>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// SERVICIOS DE PEDIDOS
builder.Services.AddScoped<PedidoService>();
builder.Services.AddHttpClient<PedidoApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiPedidos:BaseUrl"]!);
});

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

// IMPORTANTE: El orden de estas líneas es estricto y obligatorio.
// - UseAuthentication(): Lee la cookie "PizzeriaAuth" que envía el navegador, la descifra y reconstruye la identidad del usuario (User).
// - UseAuthorization(): Revisa si el usuario actual tiene permisos para entrar a las rutas protegidas con [Authorize].
app.UseAuthentication();
app.UseAuthorization();

// ----------------------------------------------------------------------
// ENDPOINTS DE API MINIMAL (Comunicación con Cocina)
// ----------------------------------------------------------------------
// El POST /pedidos fue reemplazado por el controlador REST api/pedidos.

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

// 3. CONTROLADORES DE API REST (api/pedidos)
app.MapControllers();

// 4. MAPEO DE RUTAS PARA LOS CONTROLADORES MVC (Home, Account, Pedido, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();