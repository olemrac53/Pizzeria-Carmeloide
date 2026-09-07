


DROP DATABASE IF EXISTS 5to_PizzeriaDB;
CREATE DATABASE 5to_PizzeriaDB;
USE 5to_PizzeriaDB;

-- 2. TABLA FUERTE: Clientes

CREATE TABLE Clientes (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Direccion VARCHAR(255) NOT NULL
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
    PedidoId INT NOT NULL, 
    PizzaId INT NOT NULL,  
    Cantidad INT NOT NULL DEFAULT 1,
    -- Restricciones de integridad referencial
    FOREIGN KEY (PedidoId) REFERENCES Pedidos(Id) ON DELETE CASCADE,
    FOREIGN KEY (PizzaId) REFERENCES Pizzas(Id) ON DELETE CASCADE
);