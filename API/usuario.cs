namespace PizzeriaBackend.Models;

// Cada fila de la tabla Usuarios de MySQL es un objeto Usuario.
public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // NUNCA se guarda la contraseña real: solo su "hash" (explicación en PasswordService.cs)
    public string PasswordHash { get; set; } = string.Empty;

    public string Rol { get; set; } = "Cliente";
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
