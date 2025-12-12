-- 1. РАБОТА С ПОЛЬЗОВАТЕЛЯМИ
-- ===========================================
-- Проверка логина и пароля
SELECT id, username, full_name, role_id, is_active 
FROM users 
WHERE username = 'stas2006' 
AND password_hash = crypt('stas2006', password_hash);

-- Получение роли пользователя
SELECT u.username, u.full_name, r.name as role_name
FROM users u
JOIN roles r ON u.role_id = r.id
WHERE u.username = 'stas2006';

-- Деактивировать пользователя
UPDATE users
SET is_active = false
WHERE username = 'stas2006';

-- Активировать пользователя
UPDATE users
SET is_active = true
WHERE username = 'stas2006';

-- Создание представления
create view users_with_roles as
select u.username, u.full_name as full_name, r."name"  as role_name
from users u 
join roles r on u.role_id = r.id;

-- Объединяем активных и неактивных менеджеров
SELECT 'active' as status, username, full_name
FROM users 
WHERE is_active = true AND role_id = (SELECT id FROM roles WHERE name = 'store_manager')
UNION
SELECT 'inactive' as status, username, full_name  
FROM users
WHERE is_active = false AND role_id = (SELECT id FROM roles WHERE name = 'store_manager');


-- 2. РАБОТА С ТОВАРАМИ
-- ===========================================
-- Получить все товары с категориями
SELECT p.id, p.name, p.description, p.price, p.quantity_in_stock, c.name as category
FROM products p
JOIN categories c ON p.category_id = c.id
ORDER BY p.price DESC;

-- Товары определенной категории
SELECT p.name, p.price, p.quantity_in_stock
FROM products p
JOIN categories c ON p.category_id = c.id
WHERE c.name = 'Спиннинги';

-- Поиск товаров по названию
SELECT name, price, quantity_in_stock
FROM products 
WHERE name ILIKE '%shimano%';

--Товары с низким запасом (меньше 500)
select name, quantity_in_stock
from products
where quantity_in_stock < 500
order by quantity_in_stock asc;

-- Добавить товары на склад
CREATE OR REPLACE FUNCTION update_product_quantity(
    p_product_id UUID,
    p_quantity_change INTEGER
) 
RETURNS TABLE(
    success BOOLEAN,
    message TEXT,
    new_quantity INTEGER
) AS $$
DECLARE
    current_quantity INTEGER;
BEGIN
    -- Пытаемся обновить
    UPDATE products 
    SET quantity_in_stock = quantity_in_stock + p_quantity_change
    WHERE id = p_product_id
    RETURNING quantity_in_stock INTO current_quantity;
    
    IF current_quantity IS NOT NULL THEN
        RETURN QUERY SELECT true, 'Количество обновлено', current_quantity;
    ELSE
        RETURN QUERY SELECT false, 'Товар не найден', 0;
    END IF;
END;
$$ LANGUAGE plpgsql;

select update_product_quantity(
	'7d549fd1-b4fa-4950-b457-f94366f63353',
	3
);

--Обновить цену товара
CREATE OR REPLACE FUNCTION update_product_price(
    p_product_id UUID,
    p_new_price DECIMAL(12,2)
) 
RETURNS TABLE(
    success BOOLEAN,
    message TEXT,
    old_price DECIMAL(12,2),
    new_price DECIMAL(12,2)
) AS $$
DECLARE
    v_old_price DECIMAL(12,2);
BEGIN
    SELECT price INTO v_old_price 
    FROM products 
    WHERE id = p_product_id;
    
    IF v_old_price IS NULL THEN
        RETURN QUERY SELECT false, 'Товар не найден', 0, 0;
        RETURN;
    END IF;
    
    UPDATE products 
    SET price = p_new_price
    WHERE id = p_product_id;
    
    RETURN QUERY SELECT true, 'Цена товара обновлена', v_old_price, p_new_price;
END;
$$ LANGUAGE plpgsql;

select update_product_price(
	'7d549fd1-b4fa-4950-b457-f94366f63353',
	1420.80
);

-- SELF JOIN
-- Представление
create view products_with_products as
select c.name as category ,p1.name as product1, p2.name as product2, p1.price as price1, p2.price as price2
from products p1
join products p2 on p1.category_id = p2.category_id
and p1.price < p2.price
join categories c on p1.category_id = c.id;


-- 3. ЗАКАЗЫ И ПРОДАЖИ
-- ===========================================
-- Все заказы магазина
SELECT o.id, o.total_amount, o.created_at, s.name as store_name
FROM orders o
JOIN stores s ON o.store_id = s.id
WHERE s.name = 'Крючок и пуля';

-- Детали заказа с товарами
SELECT o.id as order_id, p.name as product_name, oi.quantity, oi.unit_price
FROM orders o
JOIN order_items oi ON o.id = oi.order_id
JOIN products p ON oi.product_id = p.id
WHERE o.id = 1;

-- Сумма заказов по всем магазинам
SELECT s.name as store_name, COUNT(o.id) as order_count, SUM(o.total_amount) as total_sales
FROM stores s
LEFT JOIN orders o ON s.id = o.store_id
GROUP BY s.id, s.name
ORDER BY total_sales DESC;

-- Заказы конкреьных магазинов за период с суммой больше указанной
-- Запрос с несколькими условиями
select s.name, o.created_at, o.total_amount
from orders o
join stores s on s.id = o.store_id
where o.created_at between '2024-01-01' and '2024-01-31'
and o.total_amount > 5000
and s."name" in ('Крючок и пуля', 'КлёвоТут', 'Блесна')
order by o.total_amount desc;

-- Все товары, которые никогда не заказывали
-- Запрос с вложенной конструкцией
select p."name", p.price  
from products p
where p.id not in (
	select distinct oi.product_id 
	from order_items oi
);

-- Пронумеровать товары внутри каждой категории по цене
-- Оконные функции
SELECT 
    name,
    price,
    category_id,
    ROW_NUMBER() OVER (PARTITION BY category_id ORDER BY price DESC) as rank_in_category
FROM products;



-- 4. РАБОТА С ФИНАНСАМИ
-- ===========================================
-- Балансы всех счетов
SELECT s.name as store_name, sa.balance as store_balance
FROM stores s
JOIN store_accounts sa ON s.id = sa.store_id

-- Все транзакции
SELECT t.id, s.name as store, t.amount, t.store_account_id
FROM transactions t
JOIN store_accounts sa ON t.store_account_id = sa.id
JOIN stores s ON sa.store_id = s.id
ORDER BY t.id DESC;


-- 5. ОТЧЕТЫ И АНАЛИТИКА
-- ===========================================
-- Самые популярные товары (в смысле те, которых заказали больше всего штук по всем заказам)
SELECT p.name, SUM(oi.quantity) as total_sold, SUM(oi.quantity * oi.unit_price) as total_revenue
FROM order_items oi
JOIN products p ON oi.product_id = p.id
GROUP BY p.id, p.name
ORDER BY total_sold DESC
LIMIT 10;


-- Ранжирование товаров по цене и по количеству на складе на группы
SELECT 
    name,
    price,
    CASE 
        WHEN price > 2000 THEN 'Премиум'
        WHEN price BETWEEN 1000 AND 2000 THEN 'Средний'
        WHEN price BETWEEN 500 AND 999 THEN 'Бюджетный'
        ELSE 'Эконом'
    END as price_category,
    CASE 
        WHEN quantity_in_stock = 0 THEN 'Нет в наличии'
        WHEN quantity_in_stock < 10 THEN 'Мало'
        ELSE 'В наличии'
    END as stock_status
FROM products;



-- 6. АУДИТ И ЛОГИ
-- ===========================================
-- Последние действия пользователей
SELECT u.username, al.details, al.performed_at
FROM audit_log al
LEFT JOIN users u ON al.user_id = u.id
ORDER BY al.performed_at DESC
LIMIT 20;

-- Действия конкретного пользователя
SELECT details, performed_at
FROM audit_log
WHERE user_id = (SELECT id FROM users WHERE username = 'stas2006')
ORDER BY performed_at DESC;


-- 7. УПРАВЛЕНИЕ МАГАЗИНАМИ
-- ===========================================
-- Информация о магазине и его менеджере
SELECT s.name as store_name, s.address, s.tax_id, u.full_name as manager_name
FROM stores s
JOIN users u ON s.manager_id = u.id;

-- Магазины с их балансами
SELECT s.name, sa.balance, u.username as manager
FROM stores s
JOIN store_accounts sa ON s.id = sa.store_id
JOIN users u ON s.manager_id = u.id;








-- 8. Обзор запросов
-- ===========================================
EXPLAIN (ANALYZE, COSTS, VERBOSE)
SELECT * FROM users WHERE username = 'admin';


