-- Триггер для "мягкого" удаления пользователей (деактивация вместо удаления)
CREATE OR REPLACE FUNCTION soft_delete_user()
RETURNS TRIGGER AS $$
BEGIN
    -- Вместо удаления - помечаем как неактивного
    UPDATE users 
    SET is_active = false 
    WHERE id = OLD.id;
    
    -- Отменяем фактическое удаление
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_soft_delete_user
    BEFORE DELETE ON users
    FOR EACH ROW
    EXECUTE FUNCTION soft_delete_user();



-- Триггер для проверки существования магазина и активности его менеджера перед созданием заказа --
CREATE OR REPLACE FUNCTION check_store_and_manager_before_order()
RETURNS TRIGGER AS $$
DECLARE
    v_store_name TEXT;
    v_manager_id UUID;
    v_manager_username TEXT;
    v_manager_active BOOLEAN;
BEGIN
    -- Получаем информацию о магазине и менеджере
    SELECT s.name, s.manager_id, u.username, u.is_active
    INTO v_store_name, v_manager_id, v_manager_username, v_manager_active
    FROM stores s
    LEFT JOIN users u ON s.manager_id = u.id
    WHERE s.id = NEW.store_id;
    
    -- Проверяем существование магазина
    IF v_store_name IS NULL THEN
        RAISE EXCEPTION 'Магазин с ID % не найден', NEW.store_id;
    END IF;
    
    -- Проверяем существование менеджера
    IF v_manager_id IS NULL OR v_manager_username IS NULL THEN
        RAISE EXCEPTION 'Менеджер не назначен для магазина %', v_store_name;
    END IF;
    
    -- Проверяем активность менеджера
    IF NOT v_manager_active THEN
        RAISE EXCEPTION 
            'Менеджер "%" (ID: %) не активен. Пользователь заблокирован или деактивирован. Магазин: %', 
            v_manager_username, v_manager_id, v_store_name;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER before_order_insert_check_store
BEFORE INSERT ON orders
FOR EACH ROW
EXECUTE FUNCTION check_store_and_manager_before_order();



-- Триггер для логирования создания заказа --
CREATE OR REPLACE FUNCTION log_order_creation()
RETURNS TRIGGER AS $$
DECLARE
    v_store_name TEXT;
    v_manager_id UUID;
BEGIN
    -- Получаем информацию о магазине и менеджере
    SELECT s.name, s.manager_id
    INTO v_store_name, v_manager_id
    FROM stores s
    WHERE s.id = NEW.store_id;
    
    -- Логируем создание заказа с информацией о менеджере
    INSERT INTO audit_log (details, performed_at, user_id)
    VALUES (
        'Создан заказ №' || NEW.id || 
        ' для магазина ' || v_store_name,
        NOW(),
		v_manager_id
    );
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER after_order_insert_log
AFTER INSERT ON orders
FOR EACH ROW
WHEN (NEW.total_amount = 0.00) -- Логируем только пустые заказы
EXECUTE FUNCTION log_order_creation();



-- Триггеры для обновления суммы заказа при операцией над order_item (добавление, изменение, удаление)
CREATE OR REPLACE FUNCTION update_order_total_on_items_change()
RETURNS TRIGGER AS $$
DECLARE
    new_total DECIMAL(12,2);
BEGIN
    DECLARE
        affected_order_id UUID;
    BEGIN
        IF TG_OP = 'DELETE' THEN
            affected_order_id := OLD.order_id;
        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD.order_id != NEW.order_id THEN
                UPDATE orders 
                SET total_amount = (
                    SELECT COALESCE(SUM(quantity * unit_price), 0)
                    FROM order_items 
                    WHERE order_id = OLD.order_id
                )
                WHERE id = OLD.order_id;
                
                affected_order_id := NEW.order_id;
            ELSE
                affected_order_id := NEW.order_id;
            END IF;
        ELSE
            affected_order_id := NEW.order_id;
        END IF;
        
        SELECT COALESCE(SUM(quantity * unit_price), 0)
        INTO new_total
        FROM order_items
        WHERE order_id = affected_order_id;
        
        UPDATE orders 
        SET total_amount = new_total
        WHERE id = affected_order_id;
    END;
    
    RETURN CASE
        WHEN TG_OP = 'DELETE' THEN OLD
        ELSE NEW
    END;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_total_on_insert
    AFTER INSERT ON order_items
    FOR EACH ROW
    EXECUTE FUNCTION update_order_total_on_items_change();

CREATE TRIGGER trg_update_total_on_update
    AFTER UPDATE ON order_items
    FOR EACH ROW
    EXECUTE FUNCTION update_order_total_on_items_change();

CREATE TRIGGER trg_update_total_on_delete
    AFTER DELETE ON order_items
    FOR EACH ROW
    EXECUTE FUNCTION update_order_total_on_items_change();



-- Триггер для проверки активности менеджера перед транзакцией
CREATE OR REPLACE FUNCTION check_manager_active_before_transaction()
RETURNS TRIGGER AS $$
DECLARE
    v_store_id UUID;
    v_manager_id UUID;
    v_manager_active BOOLEAN;
    v_manager_username TEXT;
    v_store_name TEXT;
BEGIN
    -- Получаем информацию о магазине и менеджере
    SELECT s.id, s.manager_id, u.is_active, u.username, s.name
    INTO v_store_id, v_manager_id, v_manager_active, v_manager_username, v_store_name
    FROM store_accounts sa
    JOIN stores s ON sa.store_id = s.id
    LEFT JOIN users u ON s.manager_id = u.id
    WHERE sa.id = NEW.store_account_id;
    
    -- Проверяем, найден ли магазин
    IF v_store_id IS NULL THEN
        RAISE EXCEPTION 'Магазин не найден для счёта %', NEW.store_account_id;
    END IF;
    
    -- Проверяем, назначен ли менеджер
    IF v_manager_id IS NULL THEN
        RAISE EXCEPTION 
            'У магазина "%" (ID: %) не назначен менеджер. Невозможно выполнить операцию.', 
            v_store_name, v_store_id;
    END IF;
    
    -- Проверяем активность менеджера
    IF NOT v_manager_active THEN
        RAISE EXCEPTION 
            'Менеджер "%" (ID: %) магазина "%" не активен. Перевод невозможен.', 
            v_manager_username, v_manager_id, v_store_name;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER before_transaction_check_manager
BEFORE INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION check_manager_active_before_transaction();



-- Триггер для логирования транзакции
CREATE OR REPLACE FUNCTION log_manager_status_on_transaction()
RETURNS TRIGGER AS $$
DECLARE
    v_manager_active BOOLEAN;
    v_manager_username TEXT;
    v_store_name TEXT;
	v_manager_id UUID;
BEGIN
    -- Получаем информацию о менеджере
    SELECT u.is_active, u.username, s.name, u.id
    INTO v_manager_active, v_manager_username, v_store_name, v_manager_id
    FROM store_accounts sa
    JOIN stores s ON sa.store_id = s.id
    LEFT JOIN users u ON s.manager_id = u.id
    WHERE sa.id = NEW.store_account_id;
    
    -- Логируем статус менеджера
    INSERT INTO audit_log (details, performed_at, user_id)
    VALUES (
        'Проведена транзакция:' || 
		' магазин: ' || COALESCE(v_store_name, 'неизвестен') ||
        ' менеджер: ' || COALESCE(v_manager_username, 'не назначен') || 
		' сумма: ' || NEW.amount,
        NOW(),
		v_manager_id
    );
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER after_transaction_log
AFTER INSERT ON transactions
FOR EACH ROW
EXECUTE FUNCTION log_manager_status_on_transaction();



-- Просмотр всех триггеров в бд
SELECT 
    event_object_table as table_name,
    trigger_name,
    event_manipulation as event,
    action_timing as timing,
    action_statement as function
FROM information_schema.triggers
ORDER BY table_name, trigger_name;



-- проверка триггера мягкого удаления пользователя --
select u.username, u.is_active 
from users u 
where u.username = 'stas2006';

delete
from users u
where u.username = 'stas2006';

select u.username, u.is_active 
from users u 
where u.username = 'stas2006';

update users u 
set is_active = true
where u.username = 'stas2006';

select u.username, u.is_active 
from users u 
where u.username = 'stas2006';

-- проверка остальных триггеров в файле Script-functions.sql --












