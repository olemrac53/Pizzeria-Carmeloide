using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PizzeriaBackend.Models;

namespace PizzeriaBackend.Services;

// Crea los tokens JWT. Un JWT tiene 3 partes separadas por puntos: cabecera.datos.firma
//  - datos (payload): quién es el usuario y cuándo vence. Va codificado, NO cifrado: no poner contraseñas.
//  - firma: se calcula con la clave secreta del servidor; si alguien cambia los datos, la firma deja de coincidir.
public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiraUtc) Crear(Usuario usuario)
    {
        var clave = _config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en appsettings.json");
        var minutos = int.TryParse(_config["Jwt:ExpiraMinutos"], out var m) ? m : 60;

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
            SecurityAlgorithms.HmacSha256);

        // Claims = los datos del usuario que viajan dentro del token
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.NombreUsuario),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };

        var expira = DateTime.UtcNow.AddMinutes(minutos);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
