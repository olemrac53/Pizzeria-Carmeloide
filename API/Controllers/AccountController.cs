using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string usuario, string clave)
        {
            // TODO: Reemplazar esta validación por tu consulta a MySQL / PizzeriaDb
            if (usuario == "admin" && clave == "1234") 
            {
                // Crear los Claims (datos del usuario guardados en la cookie)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario),
                    new Claim(ClaimTypes.Role, "Administrador")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // Iniciar sesión y crear la cookie en el navegador
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Usuario o contraseña incorrectos";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Destruir la cookie de sesión
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}