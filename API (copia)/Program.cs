using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using PizzeriaBackend.Models;
using PizzeriaBackend.Data;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PizzeriaBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. INYECCIÓN DEL SERVICIO MVC
builder.Services.AddControllersWithViews();

// CONFIGURACIÓN DE JWT (los valores están en appsettings.json, sección "Jwt")
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key en appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

// CONFIGURACIÓN DE COOKIES DE AUTENTICACIÓN
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PizzeriaAuth";
        options.Cookie.HttpOnly = true;                  // JavaScript no puede leer la cookie
        options.Cookie.SameSite = SameSiteMode.Lax;      // no se manda desde otros sitios (protege de CSRF)
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // con HTTPS real usar: Always
        options.LoginPath = "/Account/Login";            // a dónde manda a quien no inició sesión
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);  // dura 8 horas
        options.SlidingExpiration = true;                // se renueva mientras el usuario usa el sitio
    })
    // JWT: para quienes usan la API desde afuera del navegador (ClienteApp, CocinaApp, etc.)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,            // rechaza tokens vencidos
            ValidateIssuerSigningKey = true,    // rechaza tokens con firma falsa
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Política de la API: solo acepta el token JWT (la cookie no sirve para /pedidos)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiJwt", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddSingleton<JwtService>();

// CONFIGURACIÓN DE LA BASE DE DATOS
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

// 2. MIDDLEWARES NECESARIOS (IMPORTANTE: UseStaticFiles habilita la carpeta wwwroot)
app.UseStaticFiles(); 
app.UseRouting();     

// AUTENTICACIÓN Y AUTORIZACIÓN
app.UseAuthentication(); 
app.UseAuthorization(); 

// ----------------------------------------------------------------------
// ENDPOINTS DE API MINIMAL Y SOCKETS
// ----------------------------------------------------------------------

// LOGIN DE LA API: valida usuario y contraseña contra la base y devuelve un token JWT.
// Es el único endpoint de la API que no pide token.
app.MapPost("/api/auth/login", async (LoginApiDTO dto, PizzeriaDb db, JwtService jwt) =>
{
    var nombre = (dto.Usuario ?? string.Empty).Trim();
    var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombre);

    if (usuario == null || !PasswordService.Verificar(dto.Clave ?? string.Empty, usuario.PasswordHash))
        return Results.Unauthorized();

    var (token, expira) = jwt.Crear(usuario);
    return Results.Ok(new { token, tipo = "Bearer", expiraUtc = expira });
});

// Todo lo que cuelga de /pedidos exige un token válido: Authorization: Bearer <token>
var api = app.MapGroup("/pedidos").RequireAuthorization("ApiJwt");
api.MapGet("/", async (PizzeriaDb db) => 
    await db.Pedidos.Where(p => p.Activo).ToListAsync()
);

api.MapPost("/", async (PedidoDTO dto, PizzeriaDb db) => 
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

api.MapPut("/{id}/estado", async (int id, string nuevoEstado, PizzeriaDb db) => 
{
    var pedido = await db.Pedidos.FindAsync(id);
    if (pedido is null) return Results.NotFound();

    pedido.Estado = nuevoEstado;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

api.MapDelete("/{id}", async (int id, PizzeriaDb db) => 
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