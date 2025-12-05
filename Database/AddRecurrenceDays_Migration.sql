-- ================================================================
-- Migration: Add RecurrenceDays to Service Table
-- Purpose: Support dynamic service cycle recommendations in AI Assistant
-- Date: 2024
-- ================================================================

-- Step 1: Add RecurrenceDays column
ALTER TABLE Service ADD RecurrenceDays INT NULL;
GO

-- Step 2: Set default values based on service categories
-- Note: Adjust service_id values based on your actual database

-- Khám bệnh/Da liễu: 60 ngày
UPDATE Service SET RecurrenceDays = 60 WHERE service_id IN (1, 2);

-- Tiêm phòng/Vaccine: 365 ngày
UPDATE Service SET RecurrenceDays = 365 WHERE service_id IN (3, 4);

-- Phẫu thuật: NULL (không có chu kỳ cố định)
UPDATE Service SET RecurrenceDays = NULL WHERE service_id IN (5, 6);

-- Spa/Grooming/Tắm rửa: 90 ngày
UPDATE Service SET RecurrenceDays = 90 WHERE service_id IN (7, 8, 9);

-- Nha khoa: 180 ngày (nếu có)
-- UPDATE Service SET RecurrenceDays = 180 WHERE service_id IN (...);

GO

-- Step 3: Verify results
SELECT 
    service_id,
    Name,
    Category,
    RecurrenceDays,
    CASE 
        WHEN RecurrenceDays IS NULL THEN 'No cycle'
        WHEN RecurrenceDays <= 60 THEN 'Short cycle (≤60 days)'
        WHEN RecurrenceDays <= 180 THEN 'Medium cycle (61-180 days)'
        ELSE 'Long cycle (>180 days)'
    END AS CycleType
FROM Service
ORDER BY RecurrenceDays ASC, Name;

-- ================================================================
-- Rollback Script (if needed)
-- ================================================================
-- ALTER TABLE Service DROP COLUMN RecurrenceDays;
-- GO
