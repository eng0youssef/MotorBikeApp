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
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<bool> UpdateOpenStockAsync(OpenStock entity)
    {
        const string sql = @"
            UPDATE [Open_Stock]
            SET [OpenDate]=@OpenDate,[UnitID]=@UnitId,[Qty]=@Qty,[Price]=@Price,
                [Disc]=@Disc,[DiscPer]=@DiscPer,[UnitQty]=@UnitQty
            WHERE [StoreID]=@StoreId AND [ItemID]=@ItemId";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(sql, entity) > 0;
    }

    public async Task<bool> DeleteOpenStockAsync(int storeId, int itemId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(
            "DELETE FROM [Open_Stock] WHERE [StoreID]=@StoreId AND [ItemID]=@ItemId",
            new { StoreId = storeId, ItemId = itemId }) > 0;
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
}
