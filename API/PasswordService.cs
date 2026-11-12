using System.Security.Cryptography;

namespace PizzeriaBackend.Services;

// Guarda contraseñas de forma segura.
// - Hashear: convierte "1234" en un texto irreconocible (no se puede volver atrás).
// - Verificar: hashea lo que escribió el usuario y compara con lo guardado.
// Se usa PBKDF2 con "sal" aleatoria: dos usuarios con la misma clave tienen hashes distintos.
public static class PasswordService
{
    private const int TamSal = 16;
    private const int TamHash = 32;
    private const int Iteraciones = 100_000;

    // Formato guardado en la base: iteraciones.sal.hash
    public static string Hashear(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(TamSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, TamHash);
        return $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string clave, string guardado)
    {
        var partes = guardado.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iteraciones))
            return false;

        try
        {
            var sal = Convert.FromBase64String(partes[1]);
            var esperado = Convert.FromBase64String(partes[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(clave, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(actual, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
