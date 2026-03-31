USE MotorBike;

IF EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'Active' AND Object_ID = Object_ID(N'Cars'))
BEGIN
    EXEC sp_rename 'Cars.Active', 'IsStock', 'COLUMN';
END
GO
ALTER TABLE Cars ALTER COLUMN IsLocalSupplier bit NULL;
GO
