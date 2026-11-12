-- Active: 1776719132005@@127.0.0.1@3306@5to_pizzeriadb
-- 1. Crear y usar la base de datos


DROP DATABASE IF EXISTS 5to_PizzeriaDB;
CREATE DATABASE 5to_PizzeriaDB;
USE 5to_PizzeriaDB;

-- 2. TABLA FUERTE: Clientes
-- Guarda la información de las personas de forma única.
CREATE TABLE Clientes (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Direccion VARCHAR(255) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,  
    Rol VARCHAR(30) NOT NULL DEFAULT 'Cliente',
    FechaRegistro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 3. TABLA FUERTE: Pizzas
-- Es el catálogo o menú del local.
CREATE TABLE Pizzas (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Variedad VARCHAR(100) NOT NULL,
    Precio DECIMAL(10, 2) NOT NULL -- DECIMAL es ideal para manejar dinero
);

-- 4. TABLA DEPENDIENTE: Pedidos
-- Relaciona el estado del pedido con 1 solo cliente (Relación 1 a N).
CREATE TABLE Pedidos (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClienteId INT NOT NULL, -- Clave Foránea
    ActorAsignado VARCHAR(50) DEFAULT 'Cocina',
    Estado VARCHAR(50) DEFAULT 'Espera de confirmación',
    Activo TINYINT(1) DEFAULT 1, -- 1 = Activo, 0 = Borrado Lógico
    -- Restricción de integridad referencial
    FOREIGN KEY (ClienteId) REFERENCES Clientes(Id) ON DELETE CASCADE
);

-- 5. TABLA PUENTE: DetallesPedido
-- Resuelve la relación de Muchos a Muchos entre Pedidos y Pizzas.
-- Un pedido puede tener varias pizzas, y una pizza puede estar en varios pedidos.
CREATE TABLE DetallesPedido (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    PedidoId INT NOT NULL, -- Clave Foránea hacia el Ticket/Pedido
    PizzaId INT NOT NULL,  -- Clave Foránea hacia el Menú/Pizza
    Cantidad INT NOT NULL DEFAULT 1,
    -- Restricciones de integridad referencial
    FOREIGN KEY (PedidoId) REFERENCES Pedidos(Id) ON DELETE CASCADE,
    FOREIGN KEY (PizzaId) REFERENCES Pizzas(Id) ON DELETE CASCADE
);


-- 2. Cargar los datos de nuevo
INSERT INTO Pizzas (Variedad, Precio, Id) VALUES
('Muzzarella Grande', 8500.00, 1),
('Fugazzeta Rellena', 11000.00, 2),
('Napolitana Especial', 9800.00, 3),
('Calabresa', 10500.00, 4);

-- 1. Desactivar el modo seguro temporalmente
SET SQL_SAFE_UPDATES = 0;

-- 2. Ejecutar tu sentencia DELETE
DELETE FROM Pizzas WHERE Id = 1 ;

select * from Pizzas;