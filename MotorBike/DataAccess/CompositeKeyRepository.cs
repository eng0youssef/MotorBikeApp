using Dapper;
using MotorBike.Models;

namespace MotorBike.DataAccess;

/// <summary>
/// Custom repository for tables with composite primary keys
/// (Open_Stock, Stock) that cannot use the generic Repository&lt;T&gt;.
/// </summary>
public class CompositeKeyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CompositeKeyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ── Open_Stock ───────────────────────────────────────────────────────

    public async Task<IEnumerable<OpenStock>> GetAllOpenStockAsync()
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<OpenStock>("SELECT * FROM [Open_Stock]");
    }

    public async Task<OpenStock?> GetOpenStockAsync(int storeId, int itemId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<OpenStock>(
            "SELECT * FROM [Open_Stock] WHERE [StoreID] = @StoreId AND [ItemID] = @ItemId",
            new { StoreId = storeId, ItemId = itemId });
    }

    public async Task<int> InsertOpenStockAsync(OpenStock entity)
    {
        const string sql = @"
            INSERT INTO [Open_Stock] ([StoreID],[ItemID],[OpenDate],[UnitID],[Qty],[Price],[Disc],[DiscPer],[UnitQty])
            VALUES (@StoreId,@ItemId,@OpenDate,@UnitId,@Qty,@Price,@Disc,@DiscPer,@UnitQty)";

        using var db = _connectionFactory.CreateConnection();
        var result = await db.ExecuteAsync(sql, entity);
        
        // Auto-recalculate stock for this item
        await RecalcStockForItemAsync(entity.ItemId);
        
        return result;
    }

    public async Task<bool> UpdateOpenStockAsync(OpenStock entity)
    {
        const string sql = @"
            UPDATE [Open_Stock]
            SET [OpenDate]=@OpenDate,[UnitID]=@UnitId,[Qty]=@Qty,[Price]=@Price,
                [Disc]=@Disc,[DiscPer]=@DiscPer,[UnitQty]=@UnitQty
            WHERE [StoreID]=@StoreId AND [ItemID]=@ItemId";

        using var db = _connectionFactory.CreateConnection();
        var result = await db.ExecuteAsync(sql, entity) > 0;

        if (result)
            await RecalcStockForItemAsync(entity.ItemId);

        return result;
    }

    public async Task<bool> DeleteOpenStockAsync(int storeId, int itemId)
    {
        using var db = _connectionFactory.CreateConnection();
        var result = await db.ExecuteAsync(
            "DELETE FROM [Open_Stock] WHERE [StoreID]=@StoreId AND [ItemID]=@ItemId",
            new { StoreId = storeId, ItemId = itemId }) > 0;

        if (result)
            await RecalcStockForItemAsync(itemId);

        return result;
    }

    // ── Stock ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<Stock>> GetAllStockAsync()
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<Stock>("SELECT * FROM [Stock]");
    }

    public async Task<Stock?> GetStockAsync(int itemId, int storeId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<Stock>(
            "SELECT * FROM [Stock] WHERE [ItemID]=@ItemId AND [StoreID]=@StoreId",
            new { ItemId = itemId, StoreId = storeId });
    }

    public async Task<int> InsertStockAsync(Stock entity)
    {
        const string sql = "INSERT INTO [Stock] ([ItemID],[StoreID],[Qty]) VALUES (@ItemId,@StoreId,@Qty)";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<bool> UpdateStockAsync(Stock entity)
    {
        const string sql = "UPDATE [Stock] SET [Qty]=@Qty WHERE [ItemID]=@ItemId AND [StoreID]=@StoreId";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(sql, entity) > 0;
    }

    public async Task<bool> DeleteStockAsync(int itemId, int storeId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(
            "DELETE FROM [Stock] WHERE [ItemID]=@ItemId AND [StoreID]=@StoreId",
            new { ItemId = itemId, StoreId = storeId }) > 0;
    }

    // ── Stock Recalculation ──────────────────────────────────────────────

    /// <summary>
    /// يمسح كل بيانات الصنف من جدول Stock ثم يعيد حسابها
    /// من جميع الجداول (رصيد افتتاحي، شراء، استيراد، مرتجع بيع = وارد)
    /// (بيع، مرتجع شراء = صادر) — مجمّعة بالمخزن.
    /// </summary>
    public async Task RecalcStockForItemAsync(int itemId)
    {
        using var db = _connectionFactory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            // 1 — حذف كل سجلات الصنف من Stock
            await db.ExecuteAsync(
                "DELETE FROM [Stock] WHERE [ItemID] = @ItemId",
                new { ItemId = itemId }, tx);

            // 2 — تجميع كل الكميات الواردة والصادرة وإدراج الصافي
            const string sql = @"
                INSERT INTO [Stock] ([ItemID], [StoreID], [Qty])
                SELECT @ItemId, StoreID, SUM(Qty) AS Qty
                FROM (
                    SELECT [StoreID],  [QtyAll] AS Qty FROM [Open_Stock]      WHERE [ItemID] = @ItemId
                    UNION ALL
                    SELECT [StoreID],  [QtyAll] AS Qty FROM [Buy_Sub]         WHERE [ItemID] = @ItemId
                    UNION ALL
                    SELECT [StoreID],  [QtyAll] AS Qty FROM [Import_Inv_Item] WHERE [ItemID] = @ItemId
                    UNION ALL
                    SELECT [StoreID],  [QtyAll] AS Qty FROM [ReSales_Sub]     WHERE [ItemID] = @ItemId
                    UNION ALL
                    SELECT [StoreID], -[QtyAll] AS Qty FROM [Sales_Sub]       WHERE [ItemID] = @ItemId
                    UNION ALL
                    SELECT [StoreID], -[QtyAll] AS Qty FROM [ReBuy_Sub]       WHERE [ItemID] = @ItemId
                ) AS AllMovements
                GROUP BY StoreID
                HAVING SUM(Qty) <> 0";

            await db.ExecuteAsync(sql, new { ItemId = itemId }, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Supplier Old Balance ────────────────────────────────────────────

    public async Task<double> GetSupplierOldBalanceAsync(int suppId, DateTime toDate)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            DECLARE @Bal FLOAT = 
                (CASE WHEN (SELECT TOP 1 OpenDate FROM Suppliers WHERE Supp_ID = @SuppId) < @ToDate 
                      THEN ISNULL((SELECT Credit - Debit FROM Suppliers WHERE Supp_ID = @SuppId), 0) 
                      ELSE 0 END)
                + ISNULL((SELECT SUM(Net) FROM Buy WHERE SuppID = @SuppId AND BuyDate < @ToDate), 0)
                - ISNULL((SELECT SUM(bp.PayMoney) FROM Buy_Payments bp INNER JOIN Buy b ON bp.BuyId = b.Buy_ID WHERE b.SuppID = @SuppId AND bp.PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(bc.Net) FROM Buy_Car bc INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SupplierID = @SuppId AND bc.BuyDate < @ToDate), 0)
                - ISNULL((SELECT SUM(bcp.PayMoney) FROM Buy_Car_Payments bcp INNER JOIN Buy_Car bc ON bcp.BuyID = bc.Buy_ID INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SupplierID = @SuppId AND bcp.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(Net) FROM ReBuy WHERE SuppID = @SuppId AND BuyDate < @ToDate), 0)
                + ISNULL((SELECT SUM(rp.PayMoney) FROM ReBuy_Payments rp INNER JOIN ReBuy rb ON rp.BuyId = rb.Buy_ID WHERE rb.SuppID = @SuppId AND rp.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE SuppID = @SuppId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE SuppID = @SuppId AND PayDate < @ToDate), 0);
            SELECT @Bal;";

        return await db.QueryFirstOrDefaultAsync<double>(sql, new { SuppId = suppId, ToDate = toDate });
    }

    // ── Customer Old Balance ─────────────────────────────────────────────

    public async Task<double> GetCustomerOldBalanceAsync(int cusId, DateTime toDate)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            DECLARE @Bal FLOAT = 
                (CASE WHEN (SELECT TOP 1 OpenDate FROM Customers WHERE Cus_ID = @CusId) < @ToDate 
                      THEN ISNULL((SELECT Debit - Credit FROM Customers WHERE Cus_ID = @CusId), 0) 
                      ELSE 0 END)
                + ISNULL((SELECT SUM(Net) FROM Sales WHERE CusID = @CusId AND SalesDate < @ToDate), 0)
                + ISNULL((SELECT SUM(Net) FROM Sales_Car WHERE CusId = @CusId AND SalesDate < @ToDate), 0)
                - ISNULL((SELECT SUM(sp.PayMoney) FROM Sales_Payments sp INNER JOIN Sales s ON sp.SalesId = s.Sales_ID WHERE s.CusID = @CusId AND sp.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(sc.PayMoney) FROM Sales_Car_Payments sc INNER JOIN Sales_Car ss ON sc.SalesID = ss.Sales_ID WHERE ss.CusId = @CusId AND sc.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(bc.Net) FROM Buy_Car bc INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SourceCustomerID = @CusId AND c.IsFromCustomer = 1 AND bc.BuyDate < @ToDate), 0)
                + ISNULL((SELECT SUM(bcp.PayMoney) FROM Buy_Car_Payments bcp INNER JOIN Buy_Car bc ON bcp.BuyID = bc.Buy_ID INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SourceCustomerID = @CusId AND c.IsFromCustomer = 1 AND bcp.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(Net) FROM ReSales WHERE CusID = @CusId AND SalesDate < @ToDate), 0)
                + ISNULL((SELECT SUM(rp.PayMoney) FROM ReSales_Payments rp INNER JOIN ReSales rs ON rp.SalesId = rs.Sales_ID WHERE rs.CusID = @CusId AND rp.PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId AND PayDate < @ToDate), 0);
            SELECT @Bal;";

        return await db.QueryFirstOrDefaultAsync<double>(sql, new { CusId = cusId, ToDate = toDate });
    }

    // ── Cash Old Balance ─────────────────────────────────────────────────

    public async Task<double> GetCashOldBalanceAsync(int cashId, DateTime toDate)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            DECLARE @Bal FLOAT = 
                ISNULL((SELECT Debit - Credit FROM Cash WHERE Cash_ID = @CashId), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM Sales_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM Sales_Car_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM ReBuy_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM Cash_Transfer WHERE CashTo = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Buy_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Buy_Car_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Exp_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM ReSales_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Cash_Transfer WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Import_Payments WHERE CashID = @CashId AND PayDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayTotal) FROM Import_Exp WHERE CashId = @CashId AND PayDate < @ToDate), 0)
                + ISNULL((SELECT SUM(Total) FROM Inspection WHERE CashId = @CashId AND InspDate < @ToDate), 0);
            SELECT @Bal;";

        return await db.QueryFirstOrDefaultAsync<double>(sql, new { CashId = cashId, ToDate = toDate });
    }

    // ── Supplier Balance Recalculation ───────────────────────────────────

    /// <summary>
    /// يعيد حساب رصيد المورد (Bal) من كل الحركات:
    /// رصيد افتتاحي + فواتير شراء - مدفوعات شراء - مرتجع شراء + مدفوعات مرتجع شراء - سدادات مورد
    /// </summary>
    public async Task RecalcBalanceForSupplierAsync(int suppId)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Suppliers SET Bal = 
                ISNULL(Credit, 0) - ISNULL(Debit, 0) 
                + ISNULL((SELECT SUM(Net) FROM Buy WHERE SuppID = @SuppId), 0)
                - ISNULL((SELECT SUM(bp.PayMoney) FROM Buy_Payments bp INNER JOIN Buy b ON bp.BuyId = b.Buy_ID WHERE b.SuppID = @SuppId), 0)
                + ISNULL((SELECT SUM(bc.Net) FROM Buy_Car bc INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SupplierID = @SuppId), 0)
                - ISNULL((SELECT SUM(bcp.PayMoney) FROM Buy_Car_Payments bcp INNER JOIN Buy_Car bc ON bcp.BuyID = bc.Buy_ID INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SupplierID = @SuppId), 0)
                - ISNULL((SELECT SUM(Net) FROM ReBuy WHERE SuppID = @SuppId), 0)
                + ISNULL((SELECT SUM(rp.PayMoney) FROM ReBuy_Payments rp INNER JOIN ReBuy rb ON rp.BuyId = rb.Buy_ID WHERE rb.SuppID = @SuppId), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE SuppID = @SuppId), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE SuppID = @SuppId), 0)
            WHERE Supp_ID = @SuppId";
        await db.ExecuteAsync(sql, new { SuppId = suppId });
    }

    // ── Customer Balance Recalculation ───────────────────────────────────

    /// <summary>
    /// يعيد حساب رصيد العميل (Bal) من كل الحركات:
    /// رصيد افتتاحي + فواتير بيع - مدفوعات بيع - مرتجع بيع + مدفوعات مرتجع بيع - سدادات عميل
    /// </summary>
    public async Task RecalcBalanceForCustomerAsync(int cusId)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Customers SET Bal = 
                ISNULL(Debit, 0) - ISNULL(Credit, 0) 
                + ISNULL((SELECT SUM(Net) FROM Sales WHERE CusID = @CusId), 0)
                + ISNULL((SELECT SUM(Net) FROM Sales_Car WHERE CusId = @CusId), 0)
                - ISNULL((SELECT SUM(sp.PayMoney) FROM Sales_Payments sp INNER JOIN Sales s ON sp.SalesId = s.Sales_ID WHERE s.CusID = @CusId), 0)
                - ISNULL((SELECT SUM(sc.PayMoney) FROM Sales_Car_Payments sc INNER JOIN Sales_Car ss ON sc.SalesID = ss.Sales_ID WHERE ss.CusId = @CusId), 0)
                - ISNULL((SELECT SUM(bc.Net) FROM Buy_Car bc INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SourceCustomerID = @CusId AND c.IsFromCustomer = 1), 0)
                + ISNULL((SELECT SUM(bcp.PayMoney) FROM Buy_Car_Payments bcp INNER JOIN Buy_Car bc ON bcp.BuyID = bc.Buy_ID INNER JOIN Cars c ON bc.CarID = c.Car_ID WHERE c.SourceCustomerID = @CusId AND c.IsFromCustomer = 1), 0)
                - ISNULL((SELECT SUM(Net) FROM ReSales WHERE CusID = @CusId), 0)
                + ISNULL((SELECT SUM(rp.PayMoney) FROM ReSales_Payments rp INNER JOIN ReSales rs ON rp.SalesId = rs.Sales_ID WHERE rs.CusID = @CusId), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId), 0)
            WHERE Cus_ID = @CusId";
        await db.ExecuteAsync(sql, new { CusId = cusId });
    }

    // ── Cash Balance Recalculation ───────────────────────────────────────

    /// <summary>
    /// يعيد حساب رصيد الخزينة (Bal) من كل الحركات:
    /// رصيد افتتاحي
    /// + مدفوعات بيع (وارد) + سدادات عملاء (وارد) + مدفوعات مرتجع شراء (وارد) + تحويلات واردة
    /// - مدفوعات شراء (صادر) - سدادات موردين (صادر) - مصروفات (صادر) - مدفوعات مرتجع بيع (صادر) - تحويلات صادرة
    /// </summary>
    public async Task RecalcBalanceForCashAsync(int cashId)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Cash SET Bal = 
                ISNULL(Debit, 0) - ISNULL(Credit, 0) 
                + ISNULL((SELECT SUM(PayMoney) FROM Sales_Payments WHERE CashID = @CashId), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM Sales_Car_Payments WHERE CashID = @CashId), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CashID = @CashId), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM ReBuy_Payments WHERE CashID = @CashId), 0)
                + ISNULL((SELECT SUM(PayMoney) FROM Cash_Transfer WHERE CashTo = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Buy_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Buy_Car_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE CashID = @CashId), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Supp_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Exp_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM ReSales_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Cash_Transfer WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Import_Payments WHERE CashID = @CashId), 0)
                - ISNULL((SELECT SUM(PayTotal) FROM Import_Exp WHERE CashId = @CashId), 0)
                + ISNULL((SELECT SUM(Total) FROM Inspection WHERE CashId = @CashId), 0)
            WHERE Cash_ID = @CashId";
        await db.ExecuteAsync(sql, new { CashId = cashId });
    }

    // ── Import Supplier Balance Recalculation ────────────────────────────

    /// <summary>
    /// يعيد حساب رصيد مورد الاستيراد (Bal) من كل الحركات:
    /// رصيد افتتاحي + إجمالي فواتير الاستيراد (بالعملة المحلية) - مدفوعات فواتير الاستيراد (بالعملة المحلية)
    /// </summary>
    public async Task<double> GetImportSupplierOldBalanceAsync(int suppId, DateTime toDate)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            DECLARE @Bal FLOAT = 
                (CASE WHEN (SELECT TOP 1 OpenDate FROM Import_Suppliers WHERE Supp_ID = @SuppId) < @ToDate 
                      THEN ISNULL((SELECT Credit - Debit FROM Import_Suppliers WHERE Supp_ID = @SuppId), 0) 
                      ELSE 0 END)
                + ISNULL((SELECT SUM(InvTotal) FROM Import_Invoice WHERE SuppID = @SuppId AND InvDate < @ToDate), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Import_Payments WHERE SuppID = @SuppId AND PayDate < @ToDate), 0);
            SELECT @Bal;";

        return await db.QueryFirstOrDefaultAsync<double>(sql, new { SuppId = suppId, ToDate = toDate });
    }

    public async Task RecalcBalanceForImportSupplierAsync(int suppId)
    {
        using var db = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Import_Suppliers SET Bal = 
                ISNULL(Credit, 0) - ISNULL(Debit, 0) 
                + ISNULL((SELECT SUM(InvTotal) FROM Import_Invoice WHERE SuppID = @SuppId), 0)
                - ISNULL((SELECT SUM(PayMoney) FROM Import_Payments WHERE SuppID = @SuppId), 0)
            WHERE Supp_ID = @SuppId";
        await db.ExecuteAsync(sql, new { SuppId = suppId });
    }

    // ── Average Cost Recalculation ───────────────────────────────────────

    /// <summary>
    /// يعيد حساب متوسط التكلفة المتحرك للصنف المحدد ابتداءً من تاريخ معين.
    /// يحذف كل سجلات TblCost الخاصة بالصنف من @fromDate فما بعد، ثم يعيد بناءها
    /// بناءً على جميع حركات الصنف (رصيد افتتاحي، شراء، استيراد، مرتجع بيع كوارد؛
    /// بيع، مرتجع شراء كصادر)، ويحدّث AvrgCost في جدول Items.
    /// 
    /// استخدم DateTime.MinValue لإعادة حساب كل الحركات من البداية.
    /// </summary>
    public async Task RecalcAvgCostForItemAsync(int itemId, DateTime fromDate)
    {
        // SQL Server DATETIME لا يقبل تواريخ قبل 1753. 
        // DateTime.MinValue تسبب خطأ overflow.
        if (fromDate < new DateTime(1753, 1, 1))    
            fromDate = new DateTime(1753, 1, 1);

        // SQL query that gathers all movements for the item from @FromDate, ordered by date then type.
 
        const string gatherSql = @"
            SELECT MyType, MyID, MyDate, ItemID, Qty, Cost, TotalCost
            FROM (
                -- رصيد افتتاحي (وارد)
                SELECT
                    1001 AS MyType,
                    os.ItemID AS MyID,
                    os.OpenDate AS MyDate,
                    os.ItemID,
                    SUM(os.QtyAll) AS Qty,
                    CASE WHEN SUM(os.QtyAll) <> 0 THEN SUM(os.Total) / SUM(os.QtyAll) ELSE 0 END AS Cost,
                    SUM(os.Total) AS TotalCost
                FROM Open_Stock os
                WHERE os.ItemID = @ItemId AND os.OpenDate >= @FromDate
                GROUP BY os.ItemID, os.OpenDate

                UNION ALL

                -- شراء (وارد)
                SELECT
                    1002 AS MyType,
                    b.Buy_ID AS MyID,
                    b.BuyDate AS MyDate,
                    bs.ItemID,
                    SUM(bs.QtyAll) AS Qty,
                    CASE WHEN SUM(bs.QtyAll) = 0 THEN 0
                         ELSE SUM(bs.Total * b.NetPer) / SUM(bs.QtyAll) END AS Cost,
                    SUM(bs.Total * b.NetPer) AS TotalCost
                FROM Buy b
                INNER JOIN Buy_Sub bs ON b.Buy_ID = bs.BuyId
                WHERE bs.ItemID = @ItemId AND b.BuyDate >= @FromDate
                GROUP BY b.Buy_ID, b.BuyDate, bs.ItemID

                UNION ALL

                -- استيراد (وارد)
                SELECT
                    1003 AS MyType,
                    ii.Inv_ID AS MyID,
                    ii.InvDate AS MyDate,
                    im.ItemID,
                    SUM(im.QtyAll) AS Qty,
                    CASE WHEN SUM(im.QtyAll) = 0 THEN 0
                         ELSE SUM(im.CostTotal) / SUM(im.QtyAll) END AS Cost,
                    SUM(im.CostTotal) AS TotalCost
                FROM Import_Invoice ii
                INNER JOIN Import_Inv_Item im ON ii.Inv_ID = im.InvID
                WHERE im.ItemID = @ItemId AND ii.InvDate >= @FromDate
                GROUP BY ii.Inv_ID, ii.InvDate, im.ItemID

                UNION ALL

                -- مرتجع بيع (وارد)
                SELECT
                    1007 AS MyType,
                    rs.Sales_ID AS MyID,
                    rs.SalesDate AS MyDate,
                    rss.ItemID,
                    SUM(rss.QtyAll) AS Qty,
                    CASE WHEN SUM(rss.QtyAll) = 0 THEN 0
                         ELSE SUM(rss.Total * rs.NetPer) / SUM(rss.QtyAll) END AS Cost,
                    SUM(rss.Total * rs.NetPer) AS TotalCost
                FROM ReSales rs
                INNER JOIN ReSales_Sub rss ON rs.Sales_ID = rss.SalesId
                WHERE rss.ItemID = @ItemId AND rs.SalesDate >= @FromDate
                GROUP BY rs.Sales_ID, rs.SalesDate, rss.ItemID

                UNION ALL

                -- بيع (صادر — الكميات سالبة)
                SELECT
                    1009 AS MyType,
                    s.Sales_ID AS MyID,
                    s.SalesDate AS MyDate,
                    ss.ItemID,
                    SUM(ss.QtyAll) * -1 AS Qty,
                    CASE WHEN SUM(ss.QtyAll) = 0 THEN 0
                         ELSE SUM(ss.Total * s.NetPer) / SUM(ss.QtyAll) * -1 END AS Cost,
                    SUM(ss.Total * s.NetPer) AS TotalCost
                FROM Sales s
                INNER JOIN Sales_Sub ss ON s.Sales_ID = ss.SalesId
                WHERE ss.ItemID = @ItemId AND s.SalesDate >= @FromDate
                GROUP BY s.Sales_ID, s.SalesDate, ss.ItemID

                UNION ALL

                -- مرتجع شراء (صادر — الكميات سالبة)
                SELECT
                    1011 AS MyType,
                    rb.Buy_ID AS MyID,
                    rb.BuyDate AS MyDate,
                    rbs.ItemID,
                    SUM(rbs.QtyAll) * -1 AS Qty,
                    CASE WHEN SUM(rbs.QtyAll) = 0 THEN 0
                         ELSE SUM(rbs.Total * rb.NetPer) / SUM(rbs.QtyAll) * -1 END AS Cost,
                    SUM(rbs.Total * rb.NetPer) AS TotalCost
                FROM ReBuy rb
                INNER JOIN ReBuy_Sub rbs ON rb.Buy_ID = rbs.BuyId
                WHERE rbs.ItemID = @ItemId AND rb.BuyDate >= @FromDate
                GROUP BY rb.Buy_ID, rb.BuyDate, rbs.ItemID
            ) AS AllMovements
            ORDER BY MyDate, MyType, MyID";

        // Helper to read the running balance BEFORE @fromDate so we start from the correct base
        const string previousBalSql = @"
            SELECT TOP 1 NewQty, NewCost
            FROM TblCost
            WHERE ItemID = @ItemId AND MyDate < @FromDate
            ORDER BY MyDate DESC, ID DESC";

        using var db = _connectionFactory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            // 1 — Get the opening balance (last known state before fromDate)
            var prevRow = await db.QueryFirstOrDefaultAsync<(decimal? NewQty, decimal? NewCost)>(
                previousBalSql, new { ItemId = itemId, FromDate = fromDate }, tx);

            decimal runQty  = prevRow.NewQty  ?? 0m;
            decimal runCost = prevRow.NewCost ?? 0m;

            // 2 — Delete TblCost rows for this item from fromDate onwards
            await db.ExecuteAsync(
                "DELETE FROM TblCost WHERE ItemID = @ItemId AND MyDate >= @FromDate",
                new { ItemId = itemId, FromDate = fromDate }, tx);

            // 3 — Gather all movements
            var movements = (await db.QueryAsync<MovementRow>(
                gatherSql, new { ItemId = itemId, FromDate = fromDate }, tx)).ToList();

            // 4 — Walk through movements and write TblCost rows
            foreach (var m in movements)
            {
                decimal oldQty  = runQty;
                decimal oldCost = runCost;

                decimal addQty  = 0m, addCost  = 0m;
                decimal outQty  = 0m, outCost  = 0m;

                bool isIn  = m.MyType <= 1008;  // 1001-1008 = inbound
                bool isOut = m.MyType >= 1009;  // 1009-1012 = outbound

                if (isIn)
                {
                    addQty  = (decimal)m.Qty;
                    // For purchase/import/open-stock: use the actual cost from the transaction.
                    // For sales-return: the cost is the current running average (we're putting
                    //   stock back at the average we had when we sold it, matching the SP logic).
                    bool useTransactionCost = m.MyType <= 1003;  // OpenStock, Buy, Import
                    addCost = useTransactionCost ? (decimal)m.Cost : runCost;

                    // Recalculate running average
                    decimal newTotalCost = (runQty * runCost) + (addQty * addCost);
                    runQty  += addQty;
                    runCost  = runQty != 0 ? newTotalCost / runQty : runCost;
                }
                else // isOut
                {
                    outQty  = (decimal)(m.Qty * -1); // m.Qty is already negative, flip it
                    outCost = runCost;               // always valued at current average
                    runQty += (decimal)m.Qty;        // m.Qty is negative for out rows
                    // average cost doesn't change on outbound movements
                }

                await db.ExecuteAsync(@"
                    INSERT INTO TblCost
                        (ItemID, MyDate, MyType, MyID, OldQty, OldCost, AddQty, AddCost, OutQty, OutCost)
                    VALUES
                        (@ItemId, @MyDate, @MyType, @MyId, @OldQty, @OldCost, @AddQty, @AddCost, @OutQty, @OutCost)",
                    new
                    {
                        ItemId  = itemId,
                        m.MyDate,
                        m.MyType,
                        MyId    = m.MyID,
                        OldQty  = oldQty,
                        OldCost = oldCost,
                        AddQty  = addQty,
                        AddCost = addCost,
                        OutQty  = outQty,
                        OutCost = outCost
                    }, tx);
            }

            // 5 — Update AvrgCost on the Items table
            await db.ExecuteAsync(@"
                UPDATE Items SET
                    AvrgCost = ISNULL((SELECT TOP 1 NewCost FROM TblCost WHERE ItemID = @ItemId ORDER BY MyDate DESC, ID DESC), 0)
                WHERE Item_ID = @ItemId",
                new { ItemId = itemId }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Internal DTO for average-cost movements query.</summary>
    private sealed record MovementRow(
        int      MyType,
        int      MyID,
        DateTime MyDate,
        int      ItemID,
        double   Qty,
        double   Cost,
        double   TotalCost);
}
