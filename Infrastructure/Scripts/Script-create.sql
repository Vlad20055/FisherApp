-- Таблица ролей
CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

ALTER TABLE roles 
ADD CONSTRAINT roles_name_length CHECK (length(name) BETWEEN 3 AND 50)

-- Таблица пользователей
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    full_name TEXT NOT NULL,
    role_id INT NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    is_active BOOLEAN DEFAULT FALSE
);

alter table users
add constraint username_length check (length(username) between 3 and 50),
ADD CONSTRAINT full_name_length CHECK (length(full_name) BETWEEN 3 AND 100);

-- Для поиска пользователей по роли
CREATE INDEX idx_users_role_id ON users(role_id);
-- Для поиска активных пользователей 
CREATE INDEX idx_users_is_active ON users(is_active) WHERE is_active = true;

-- Таблица магазинов
CREATE TABLE stores (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    address TEXT NOT NULL,
    tax_id TEXT NOT NULL UNIQUE,
    manager_id INT NOT NULL UNIQUE REFERENCES users(id) ON DELETE RESTRICT
);

alter table stores
ADD CONSTRAINT store_name_length CHECK (length(name) BETWEEN 2 AND 100),
ADD CONSTRAINT store_address_length CHECK (length(address) BETWEEN 5 AND 200)

-- Таблица счетов магазинов (1:1 со store)
CREATE TABLE store_accounts (
    id SERIAL PRIMARY KEY,
    balance DECIMAL(15,2) DEFAULT 0.0 CHECK (balance >= 0),
    store_id INT NOT NULL UNIQUE REFERENCES stores(id) ON DELETE CASCADE
);

-- Таблица категорий товаров
CREATE TABLE categories (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

alter table categories 
ADD CONSTRAINT category_name_length CHECK (length(name) BETWEEN 2 AND 50)

-- Таблица товаров
CREATE TABLE products (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    price DECIMAL(12,2) NOT NULL CHECK (price >= 0),
    quantity_in_stock INT DEFAULT 0 CHECK (quantity_in_stock >= 0),
    category_id INT NOT NULL REFERENCES categories(id) ON DELETE RESTRICT
);

alter table products
ADD CONSTRAINT product_name_length CHECK (length(name) BETWEEN 2 AND 200);

alter table products 
add constraint product_name_unique unique (name);


-- Для поиска товаров по категории
CREATE INDEX idx_products_category_id ON products(category_id);

-- Таблица заказов
CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    store_id INT NOT NULL REFERENCES stores(id) ON DELETE CASCADE,
    total_amount DECIMAL(12,2) NOT NULL CHECK (total_amount >= 0),
    created_at TIMESTAMP DEFAULT now()
);

-- Для поиска заказов по магазину и дате
CREATE INDEX idx_orders_store_id ON orders(store_id);
CREATE INDEX idx_orders_created_at ON orders(created_at);

-- Таблица позиций заказа
CREATE TABLE order_items (
    id SERIAL PRIMARY KEY,
    order_id INT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id INT NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    quantity INT NOT NULL CHECK (quantity > 0),
    unit_price DECIMAL(12,2) NOT NULL CHECK (unit_price >= 0)
);

-- Для поиска позиций заказа
CREATE INDEX idx_order_items_order_id ON order_items(order_id);
CREATE INDEX idx_order_items_product_id ON order_items(product_id);

-- Таблица счета компании
CREATE TABLE company_accounts (
    id SERIAL PRIMARY KEY,
    balance DECIMAL(15,2) DEFAULT 0.0 CHECK (balance >= 0)
);

-- Таблица транзакций
CREATE TABLE transactions (
    id SERIAL PRIMARY KEY,
    store_account_id INT NOT NULL REFERENCES store_accounts(id) ON DELETE CASCADE,
    company_account_id INT NOT NULL REFERENCES company_accounts(id) ON DELETE CASCADE,
    amount DECIMAL(15,2) NOT NULL CHECK (amount <> 0)
);

-- Таблица журнала аудита
CREATE TABLE audit_log (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id) ON DELETE SET NULL,
    details TEXT,
    performed_at TIMESTAMP DEFAULT now()
);


-- Теперь мы хотим перейти на uuid вместо int id
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Добавляем новый столбец uuid
ALTER TABLE roles ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE users ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE stores ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE store_accounts ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE categories ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE products ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE orders ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE order_items ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE company_accounts ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE transactions ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();
ALTER TABLE audit_log ADD COLUMN uuid UUID DEFAULT uuid_generate_v4();

-- Сначала добавляем uuid в ссылочные поля
ALTER TABLE users ADD COLUMN role_uuid UUID;
ALTER TABLE stores ADD COLUMN manager_uuid UUID;
ALTER TABLE store_accounts ADD COLUMN store_uuid UUID;
ALTER TABLE products ADD COLUMN category_uuid UUID;
ALTER TABLE orders ADD COLUMN store_uuid UUID;
ALTER TABLE order_items ADD COLUMN order_uuid UUID;
ALTER TABLE order_items ADD COLUMN product_uuid UUID;
ALTER TABLE transactions ADD COLUMN store_account_uuid UUID;
ALTER TABLE transactions ADD COLUMN company_account_uuid UUID;
ALTER TABLE audit_log ADD COLUMN user_uuid UUID;

-- Обновляем role_uuid в users
UPDATE users SET role_uuid = r.uuid
FROM roles r WHERE users.role_id = r.id;

-- Обновляем manager_uuid в stores  
UPDATE stores SET manager_uuid = u.uuid
FROM users u WHERE stores.manager_id = u.id;

-- И так для всех таблиц...
UPDATE store_accounts SET store_uuid = s.uuid
FROM stores s WHERE store_accounts.store_id = s.id;

UPDATE products SET category_uuid = c.uuid
FROM categories c WHERE products.category_id = c.id;

UPDATE orders SET store_uuid = s.uuid
FROM stores s WHERE orders.store_id = s.id;

UPDATE order_items SET order_uuid = o.uuid
FROM orders o WHERE order_items.order_id = o.id;

UPDATE order_items SET product_uuid = p.uuid
FROM products p WHERE order_items.product_id = p.id;

UPDATE transactions 
SET store_account_uuid = sa.uuid
FROM store_accounts sa 
WHERE transactions.store_account_id = sa.id;

UPDATE transactions 
SET company_account_uuid = ca.uuid
FROM company_accounts ca 
WHERE transactions.company_account_id = ca.id;

UPDATE audit_log 
SET user_uuid = u.uuid
FROM users u 
WHERE audit_log.user_id = u.id;


-- Теперь нужно поменять primary keys
-- 1. Удаляем старые PRIMARY KEY с CASCADE
ALTER TABLE roles DROP CONSTRAINT roles_pkey CASCADE;
ALTER TABLE users DROP CONSTRAINT users_pkey CASCADE;
ALTER TABLE stores DROP CONSTRAINT stores_pkey CASCADE;
ALTER TABLE store_accounts DROP CONSTRAINT store_accounts_pkey CASCADE;
ALTER TABLE categories DROP CONSTRAINT categories_pkey CASCADE;
ALTER TABLE products DROP CONSTRAINT products_pkey CASCADE;
ALTER TABLE orders DROP CONSTRAINT orders_pkey CASCADE;
ALTER TABLE order_items DROP CONSTRAINT order_items_pkey CASCADE;
ALTER TABLE company_accounts DROP CONSTRAINT company_accounts_pkey CASCADE;
ALTER TABLE transactions DROP CONSTRAINT transactions_pkey CASCADE;
ALTER TABLE audit_log DROP CONSTRAINT audit_log_pkey CASCADE;

-- 2. Создаем новые PRIMARY KEY на uuid
ALTER TABLE roles ADD PRIMARY KEY (uuid);
ALTER TABLE users ADD PRIMARY KEY (uuid);
ALTER TABLE stores ADD PRIMARY KEY (uuid);
ALTER TABLE store_accounts ADD PRIMARY KEY (uuid);
ALTER TABLE categories ADD PRIMARY KEY (uuid);
ALTER TABLE products ADD PRIMARY KEY (uuid);
ALTER TABLE orders ADD PRIMARY KEY (uuid);
ALTER TABLE order_items ADD PRIMARY KEY (uuid);
ALTER TABLE company_accounts ADD PRIMARY KEY (uuid);
ALTER TABLE transactions ADD PRIMARY KEY (uuid);
ALTER TABLE audit_log ADD PRIMARY KEY (uuid);

-- Пользователи → Роли
ALTER TABLE users ADD CONSTRAINT fk_users_role_id 
    FOREIGN KEY (role_uuid) REFERENCES roles(uuid) ON DELETE RESTRICT;

-- Магазины → Менеджеры (пользователи)  
ALTER TABLE stores ADD CONSTRAINT fk_stores_manager_id 
    FOREIGN KEY (manager_uuid) REFERENCES users(uuid) ON DELETE RESTRICT;

-- Счета магазинов → Магазины
ALTER TABLE store_accounts ADD CONSTRAINT fk_store_accounts_store_id 
    FOREIGN KEY (store_uuid) REFERENCES stores(uuid) ON DELETE CASCADE;

-- Товары → Категории
ALTER TABLE products ADD CONSTRAINT fk_products_category_id 
    FOREIGN KEY (category_uuid) REFERENCES categories(uuid) ON DELETE RESTRICT;

-- Заказы → Магазины
ALTER TABLE orders ADD CONSTRAINT fk_orders_store_id 
    FOREIGN KEY (store_uuid) REFERENCES stores(uuid) ON DELETE CASCADE;

-- Позиции заказа → Заказы
ALTER TABLE order_items ADD CONSTRAINT fk_order_items_order_id 
    FOREIGN KEY (order_uuid) REFERENCES orders(uuid) ON DELETE CASCADE;

-- Позиции заказа → Товары  
ALTER TABLE order_items ADD CONSTRAINT fk_order_items_product_id 
    FOREIGN KEY (product_uuid) REFERENCES products(uuid) ON DELETE RESTRICT;

-- Транзакции → Счета магазинов
ALTER TABLE transactions ADD CONSTRAINT fk_transactions_store_account_id 
    FOREIGN KEY (store_account_uuid) REFERENCES store_accounts(uuid) ON DELETE CASCADE;

-- Транзакции → Счета компании
ALTER TABLE transactions ADD CONSTRAINT fk_transactions_company_account_id 
    FOREIGN KEY (company_account_uuid) REFERENCES company_accounts(uuid) ON DELETE CASCADE;

-- Логи аудита → Пользователи
ALTER TABLE audit_log ADD CONSTRAINT fk_audit_log_user_id 
    FOREIGN KEY (user_uuid) REFERENCES users(uuid) ON DELETE SET NULL;

-- Удаляем старые id и foreign key колонки
alter table roles drop column id;
ALTER TABLE users DROP COLUMN id, DROP COLUMN role_id;
ALTER TABLE stores DROP COLUMN id, DROP COLUMN manager_id;
ALTER TABLE store_accounts DROP COLUMN id, DROP COLUMN store_id;
ALTER TABLE categories DROP COLUMN id;
ALTER TABLE products DROP COLUMN id, DROP COLUMN category_id;
ALTER TABLE orders DROP COLUMN id, DROP COLUMN store_id;
ALTER TABLE order_items DROP COLUMN id, DROP COLUMN order_id, DROP COLUMN product_id;
ALTER TABLE company_accounts DROP COLUMN id;
ALTER TABLE transactions DROP COLUMN id, DROP COLUMN store_account_id, DROP COLUMN company_account_id;
ALTER TABLE audit_log DROP COLUMN id, DROP COLUMN user_id;

-- Переименовываем uuid в id для удобства
ALTER TABLE roles RENAME COLUMN uuid TO id;
ALTER TABLE users RENAME COLUMN uuid TO id;
ALTER TABLE users RENAME COLUMN role_uuid TO role_id;
ALTER TABLE stores RENAME COLUMN uuid TO id;
ALTER TABLE stores RENAME COLUMN manager_uuid TO manager_id;
ALTER TABLE store_accounts RENAME COLUMN uuid TO id;
ALTER TABLE store_accounts RENAME COLUMN store_uuid TO store_id;
ALTER TABLE categories RENAME COLUMN uuid TO id;
ALTER TABLE products RENAME COLUMN uuid TO id;
ALTER TABLE products RENAME COLUMN category_uuid TO category_id;
ALTER TABLE orders RENAME COLUMN uuid TO id;
ALTER TABLE orders RENAME COLUMN store_uuid TO store_id;
ALTER TABLE order_items RENAME COLUMN uuid TO id;
ALTER TABLE order_items RENAME COLUMN order_uuid TO order_id;
ALTER TABLE order_items RENAME COLUMN product_uuid TO product_id;
ALTER TABLE company_accounts RENAME COLUMN uuid TO id;
ALTER TABLE transactions RENAME COLUMN uuid TO id;
ALTER TABLE transactions RENAME COLUMN store_account_uuid TO store_account_id;
ALTER TABLE transactions RENAME COLUMN company_account_uuid TO company_account_id;
ALTER TABLE audit_log RENAME COLUMN uuid TO id;
ALTER TABLE audit_log RENAME COLUMN user_uuid TO user_id;



