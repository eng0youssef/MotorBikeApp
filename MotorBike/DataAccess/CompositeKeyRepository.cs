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
                ISNULL(Credit, 0) - ISNULL(Debit, 0) 
                - ISNULL((SELECT SUM(Net) FROM Sales WHERE CusID = @CusId), 0)
                - ISNULL((SELECT SUM(Total) FROM Sales_Car WHERE CusId = @CusId), 0)
                + ISNULL((SELECT SUM(sp.PayMoney) FROM Sales_Payments sp INNER JOIN Sales s ON sp.SalesId = s.Sales_ID WHERE s.CusID = @CusId), 0)
                + ISNULL((SELECT SUM(sc.PayMoney) FROM Sales_Car_Payments sc INNER JOIN Sales_Car ss ON sc.SalesID = ss.Sales_ID WHERE ss.CusId = @CusId), 0)
                + ISNULL((SELECT SUM(Net) FROM ReSales WHERE CusID = @CusId), 0)
                - ISNULL((SELECT SUM(rp.PayMoney) FROM ReSales_Payments rp INNER JOIN ReSales rs ON rp.SalesId = rs.Sales_ID WHERE rs.CusID = @CusId), 0)
                + ISNULL((SELECT SUM(CASE WHEN PayType = 1 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId), 0)
                - ISNULL((SELECT SUM(CASE WHEN PayType = 0 THEN PayMoney ELSE 0 END) FROM Cus_Payments WHERE CusID = @CusId), 0)
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
                ISNULL(Credit, 0) - ISNULL(Debit, 0) 
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
}
