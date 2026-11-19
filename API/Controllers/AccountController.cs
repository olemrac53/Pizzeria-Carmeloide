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
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

            if (usuario == null || !PasswordService.Verificar(model.Clave, usuario.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos");
                return View(model);
            }

            // Si el usuario es válido, se emite la cookie llamando al método auxiliar
            await IniciarSesion(usuario, model.Recordarme);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

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
                PasswordHash = PasswordService.Hashear(model.Clave),
                Rol = "Cliente"
            };

            _db.Usuarios.Add(nuevo);
            await _db.SaveChangesAsync();

            // Al registrarse con éxito, también se emite la cookie automáticamente para loguearlo al instante
            await IniciarSesion(nuevo, false);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // [COOKIES] 3. DESTRUCCIÓN DE LA COOKIE (LOGOUT)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // HttpContext.SignOutAsync borra/invalida la cookie de autenticación del navegador del cliente
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // [COOKIES] 4. MÉTODO AUXILIAR DE EMISIÓN
        // ==========================================
        private async Task IniciarSesion(Usuario usuario, bool recordarme)
        {
            // 1. Se empaquetan los datos del usuario dentro de "Claims" (identidad digital)
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
                // IsPersistent define si la cookie sobrevive al cerrar el navegador (true si marcó "Recordarme")
                IsPersistent = recordarme,
                ExpiresUtc = recordarme ? DateTimeOffset.UtcNow.AddDays(7) : (DateTimeOffset?)null
            };

            // 2. HttpContext.SignInAsync serializa los claims, los cifra y genera la cookie HTTP que se envía al navegador
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad),
                propiedades);
        }
    }
}