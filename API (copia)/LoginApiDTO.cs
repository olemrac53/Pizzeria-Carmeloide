namespace PizzeriaBackend.Models;

// Datos que manda quien quiere un token: POST /api/auth/login
public class LoginApiDTO
{
    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
}
