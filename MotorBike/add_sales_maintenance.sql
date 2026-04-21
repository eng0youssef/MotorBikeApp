IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sales_Maintenance')
BEGIN
    CREATE TABLE Sales_Maintenance (
        Id        INT NOT NULL,
        SalesId   INT NOT NULL,
        ItemName  NVARCHAR(200) NOT NULL,
        Cost      FLOAT NOT NULL DEFAULT 0,
        Price     FLOAT NOT NULL DEFAULT 0,
        IsCash    BIT NOT NULL DEFAULT 0,
        CashId    INT NULL,
        SuppId    INT NULL,
        CONSTRAINT PK_Sales_Maintenance PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT FK_Sales_Maintenance_Sales FOREIGN KEY (SalesId) REFERENCES Sales(Sales_ID) ON DELETE CASCADE
    );
END
GO
