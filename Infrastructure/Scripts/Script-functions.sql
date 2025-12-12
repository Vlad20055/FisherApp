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
	1
);



-- Обновить цену товара
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



-- Создание пустого заказа --
CREATE OR REPLACE FUNCTION create_empty_order(
    p_store_id UUID
) 
RETURNS TABLE(
    order_id UUID,
    store_name TEXT,
    order_created_at TIMESTAMP,
    success BOOLEAN,
    message TEXT
) AS $$
DECLARE
    v_new_order_id UUID;
    v_store_name TEXT;
    v_created_at TIMESTAMP;
BEGIN
    
    INSERT INTO orders (store_id, total_amount, created_at)
    VALUES (p_store_id, 0.00, NOW())
    RETURNING id, created_at INTO v_new_order_id, v_created_at;
    
    
    SELECT name INTO v_store_name FROM stores WHERE id = p_store_id;
    
    RETURN QUERY SELECT 
        v_new_order_id, 
        v_store_name, 
        v_created_at,
        true, 
        'Заказ успешно создан';
    
EXCEPTION
    WHEN others THEN
        -- Обрабатываем исключение, которое мог выбросить триггер
        RETURN QUERY SELECT 
            NULL::UUID, 
            NULL::TEXT, 
            NULL::TIMESTAMP,
            false, 
            SQLERRM; -- Возвращаем сообщение об ошибке из триггера
END;
$$ LANGUAGE plpgsql;

select * from create_empty_order(
	'293773e4-bf6c-49a9-a4e3-d0083759b0c2'
);



-- Функция для добавления товара в заказ --
CREATE OR REPLACE FUNCTION add_product_into_order(
    p_product_id UUID,
    p_quantity INTEGER,
    p_order_id UUID
) 
RETURNS TABLE(
    product_id UUID,
    quantity INTEGER,
    order_id UUID,
    unit_price NUMERIC(12,2),
    success BOOLEAN,
    message TEXT
) AS $$
DECLARE
    v_unit_price NUMERIC(12,2);
    v_order_exists BOOLEAN;
    v_product_exists BOOLEAN;
    v_product_name TEXT;
    v_store_name TEXT;
    v_order_store_id UUID;
    v_new_item_id UUID;
BEGIN
    -- Проверяем существование заказа
    SELECT EXISTS(SELECT 1 FROM orders WHERE id = p_order_id), store_id
    INTO v_order_exists, v_order_store_id
    FROM orders WHERE id = p_order_id;
    
    IF NOT v_order_exists THEN
        RETURN QUERY SELECT 
            NULL::UUID, 
            NULL::INTEGER, 
            NULL::UUID,
            NULL::NUMERIC(12,2), 
            false, 
            'Заказ с ID ' || p_order_id || ' не найден';
        RETURN;
    END IF;
    
    -- Проверяем существование товара
    SELECT EXISTS(SELECT 1 FROM products WHERE id = p_product_id), 
           price, 
           name
    INTO v_product_exists, v_unit_price, v_product_name
    FROM products WHERE id = p_product_id;
    
    IF NOT v_product_exists THEN
        RETURN QUERY SELECT 
            NULL::UUID, 
            NULL::INTEGER, 
            NULL::UUID,
            NULL::NUMERIC(12,2), 
            false, 
            'Товар с ID ' || p_product_id || ' не найден';
        RETURN;
    END IF;
    
    -- Добавляем товар в заказ
    INSERT INTO order_items (product_id, quantity, order_id, unit_price)
    VALUES (p_product_id, p_quantity, p_order_id, v_unit_price)
    RETURNING id INTO v_new_item_id;
    
    -- Возвращаем результат
    RETURN QUERY SELECT 
        p_product_id, 
        p_quantity, 
        p_order_id,
        v_unit_price, 
        true, 
        'Товар "' || v_product_name || '" успешно добавлен в заказ';
    
EXCEPTION
    WHEN others THEN
        -- Обрабатываем исключение
        RETURN QUERY SELECT 
            NULL::UUID, 
            NULL::INTEGER, 
            NULL::UUID,
            NULL::NUMERIC(12,2), 
            false, 
            SQLERRM;
END;
$$ LANGUAGE plpgsql;

select * from add_product_into_order(
	'b56cf033-4867-4957-9a09-09de36cd2dae',
	2,
	'c91c0fe9-3dc5-4789-b893-12f59bb360c9'
);



-- Функция с перевода денег со счёта магазина на счёт компании
CREATE OR REPLACE FUNCTION transfer_store_to_company(
    p_store_account_id UUID,
    p_company_account_id UUID,
    p_amount DECIMAL(12, 2)
)
RETURNS BOOLEAN AS $$
DECLARE
    v_store_balance DECIMAL(12, 2);
BEGIN
    IF p_amount <= 0 THEN
        RAISE EXCEPTION 'Сумма должна быть положительной';
    END IF;

    SELECT balance INTO v_store_balance 
    FROM store_accounts 
    WHERE id = p_store_account_id;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Счёт магазина не найден';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM company_accounts WHERE id = p_company_account_id) THEN
        RAISE EXCEPTION 'Счёт компании не найден';
    END IF;

    IF v_store_balance < p_amount THEN
        RAISE EXCEPTION 'Недостаточно средств. Доступно: %, требуется: %', v_store_balance, p_amount;
    END IF;

    UPDATE store_accounts 
    SET balance = balance - p_amount 
    WHERE id = p_store_account_id;

    UPDATE company_accounts 
    SET balance = balance + p_amount 
    WHERE id = p_company_account_id;

    INSERT INTO transactions (amount, store_account_id, company_account_id)
    VALUES (p_amount, p_store_account_id, p_company_account_id);

    RETURN TRUE;
END;
$$ LANGUAGE plpgsql;

select * from transfer_store_to_company(
	'149d9a54-2863-45e9-b7dd-fef8aed47e0c',
	'fa6597d0-03df-42c8-bc68-514b250ecdbb',
	0.5
);













