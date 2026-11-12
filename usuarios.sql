-- Crea SOLO la tabla de usuarios. No borra nada de lo que ya tenés.
USE 5to_PizzeriaDB;

CREATE TABLE IF NOT EXISTS Usuarios (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    NombreUsuario VARCHAR(50) NOT NULL UNIQUE,
    Email VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,   -- hash de la contraseña, nunca la contraseña real
    Rol VARCHAR(30) NOT NULL DEFAULT 'Cliente',
    FechaRegistro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
