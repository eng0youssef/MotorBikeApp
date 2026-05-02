-- ============================================================
-- Script: إضافة حقل CC (السعة الاسطوانية بالسم المكعب)
-- التاريخ: 2026-05-02
-- الجداول المتأثرة: Cars, Inspection
-- ============================================================

-- 1. إضافة عمود CC إلى جدول Cars
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'Cars') AND name = N'CC'
)
BEGIN
    ALTER TABLE Cars ADD CC INT NULL;
    PRINT 'تم إضافة عمود CC إلى جدول Cars بنجاح.';
END
ELSE
BEGIN
    PRINT 'عمود CC موجود مسبقاً في جدول Cars.';
END

-- 2. إضافة عمود CC إلى جدول Inspection
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'Inspection') AND name = N'CC'
)
BEGIN
    ALTER TABLE Inspection ADD CC INT NULL;
    PRINT 'تم إضافة عمود CC إلى جدول Inspection بنجاح.';
END
ELSE
BEGIN
    PRINT 'عمود CC موجود مسبقاً في جدول Inspection.';
END

PRINT 'انتهى تحديث قاعدة البيانات.';
