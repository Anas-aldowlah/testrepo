START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE sales_days ADD CONSTRAINT ck_sales_days_opening_balance_nonnegative CHECK (opening_balance >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE sales_days ADD CONSTRAINT ck_sales_days_totals_nonnegative CHECK (total_sales >= 0 AND total_cash >= 0 AND total_transfer >= 0 AND total_wallet >= 0 AND total_returns >= 0 AND net_total >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE sales ADD CONSTRAINT ck_sales_amounts_nonnegative CHECK (total_amount >= 0 AND discount_total >= 0 AND final_amount >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE sale_items ADD CONSTRAINT ck_sale_items_amounts_nonnegative CHECK (unit_price >= 0 AND discount >= 0 AND total >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE products ADD CONSTRAINT ck_products_price_nonnegative CHECK (price >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE products ADD CONSTRAINT ck_products_stockquantity_nonnegative CHECK (stockquantity >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE orders ADD CONSTRAINT ck_orders_totalamount_nonnegative CHECK (totalamount >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE orderitems ADD CONSTRAINT ck_orderitems_quantity_positive CHECK (quantity > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE orderitems ADD CONSTRAINT ck_orderitems_unitprice_nonnegative CHECK (unitprice >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    ALTER TABLE cartitems ADD CONSTRAINT ck_cartitems_quantity_nonnegative CHECK (quantity >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816135606_AddStockAndPriceCheckConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816135606_AddStockAndPriceCheckConstraints', '9.0.0');
    END IF;
END $EF$;
COMMIT;

