using System.ComponentModel.DataAnnotations;

namespace PizzeriaBackend.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Ingresá tu usuario")]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá tu contraseña")]
    [DataType(DataType.Password)]
    public string Clave { get; set; } = string.Empty;

    public bool Recordarme { get; set; }
}

public class RegistroViewModel
{
    [Required(ErrorMessage = "Elegí un nombre de usuario")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Tiene que tener entre 3 y 30 caracteres")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Solo letras, números, punto, guion y guion bajo")]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá tu email")]
    [EmailAddress(ErrorMessage = "El email no es válido")]
    [StringLength(150, ErrorMessage = "El email es demasiado largo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Elegí una contraseña")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mínimo 6 caracteres")]
    [DataType(DataType.Password)]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repetí la contraseña")]
    [Compare(nameof(Clave), ErrorMessage = "Las contraseñas no coinciden")]
    [DataType(DataType.Password)]
    public string ConfirmarClave { get; set; } = string.Empty;
}
