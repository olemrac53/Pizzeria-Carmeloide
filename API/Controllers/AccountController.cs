using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzeriaBackend.Data;
using PizzeriaBackend.Models;
using PizzeriaBackend.Services;

namespace API.Controllers
{
    public class AccountController : Controller
    {
        private readonly PizzeriaDb _db;

        public AccountController(PizzeriaDb db)
        {
            _db = db;
        }

        // ---------------------------- LOGIN ----------------------------

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var nombreUsuario = model.Usuario.Trim();

            // Busca al usuario en la base de datos (MySQL)
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

            // Mismo mensaje si falla el usuario o la clave (no damos pistas)
            if (usuario == null || !PasswordService.Verificar(model.Clave, usuario.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos");
                return View(model);
            }

            await IniciarSesion(usuario, model.Recordarme);

            // Solo redirige a direcciones de este mismo sitio (evita redirecciones maliciosas)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // --------------------------- REGISTRO --------------------------

        [HttpGet]
        public IActionResult Registro()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new RegistroViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var nombre = model.Usuario.Trim();
            var email = model.Email.Trim();

            // Que no exista otro usuario o email igual
            if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombre))
                ModelState.AddModelError(nameof(model.Usuario), "Ese nombre de usuario ya está en uso");

            if (await _db.Usuarios.AnyAsync(u => u.Email == email))
                ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con ese email");

            if (!ModelState.IsValid)
                return View(model);

            var nuevo = new Usuario
            {
                NombreUsuario = nombre,
                Email = email,
                PasswordHash = PasswordService.Hashear(model.Clave), // se guarda el hash, no la clave
                Rol = "Cliente"
            };

            _db.Usuarios.Add(nuevo);

            try
            {
                await _db.SaveChangesAsync(); // INSERT en la tabla Usuarios
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"[ERROR REGISTRO] {ex.Message}");
                ModelState.AddModelError(string.Empty, "No se pudo crear la cuenta. Probá de nuevo.");
                return View(model);
            }

            // Después de registrarse, entra directo (se crea la cookie)
            await IniciarSesion(nuevo, false);
            return RedirectToAction("Index", "Home");
        }

        // ---------------------------- LOGOUT ---------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Borra la cookie de sesión
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // ------------------------ AUXILIAR COOKIE ----------------------

        private async Task IniciarSesion(Usuario usuario, bool recordarme)
        {
            // Claims = datos del usuario que viajan dentro de la cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol)
            };

            var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var propiedades = new AuthenticationProperties
            {
                // true  = cookie persistente (sigue al cerrar el navegador)
                // false = cookie de sesión (se borra al cerrar el navegador)
                IsPersistent = recordarme,
                ExpiresUtc = recordarme ? DateTimeOffset.UtcNow.AddDays(7) : (DateTimeOffset?)null
            };

            // Crea la cookie cifrada y la manda al navegador
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad),
                propiedades);
        }
    }
}
