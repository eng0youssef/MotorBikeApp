-- =====================================================================
-- Migration: Add ExchangeRate and PayMoneyTo to Cash_Transfer table
-- ExchangeRate : سعر الصرف وقت التحويل (1 = نفس العملة)
-- PayMoneyTo   : المبلغ بعملة الخزينة الوجهة = PayMoney * ExchangeRate
-- =====================================================================

-- إضافة عمود ExchangeRate لو مش موجود
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'ExchangeRate'
      AND Object_ID = OBJECT_ID('Cash_Transfer')
)
BEGIN
    ALTER TABLE Cash_Transfer
        ADD ExchangeRate FLOAT NOT NULL DEFAULT 1;
    PRINT 'Column ExchangeRate added to Cash_Transfer.';
END
ELSE
BEGIN
    PRINT 'Column ExchangeRate already exists in Cash_Transfer.';
END
GO

-- إضافة عمود PayMoneyTo لو مش موجود
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'PayMoneyTo'
      AND Object_ID = OBJECT_ID('Cash_Transfer')
)
BEGIN
    ALTER TABLE Cash_Transfer
        ADD PayMoneyTo FLOAT NOT NULL DEFAULT 0;
    PRINT 'Column PayMoneyTo added to Cash_Transfer.';
END
ELSE
BEGIN
    PRINT 'Column PayMoneyTo already exists in Cash_Transfer.';
END
GO

-- تحديث السجلات القديمة: PayMoneyTo = PayMoney و ExchangeRate = 1 (نفس العملة)
UPDATE Cash_Transfer
SET PayMoneyTo  = PayMoney,
    ExchangeRate = 1
WHERE PayMoneyTo = 0;

PRINT 'Old records backfilled: PayMoneyTo = PayMoney, ExchangeRate = 1.';
GO

-- إضافة عمود FromRate لو مش موجود
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'FromRate'
      AND Object_ID = OBJECT_ID('Cash_Transfer')
)
BEGIN
    ALTER TABLE Cash_Transfer
        ADD FromRate FLOAT NOT NULL DEFAULT 1;
    PRINT 'Column FromRate added to Cash_Transfer.';
END
GO

-- إضافة عمود ToRate لو مش موجود
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'ToRate'
      AND Object_ID = OBJECT_ID('Cash_Transfer')
)
BEGIN
    ALTER TABLE Cash_Transfer
        ADD ToRate FLOAT NOT NULL DEFAULT 1;
    PRINT 'Column ToRate added to Cash_Transfer.';
END
GO
