using System.Reflection;
using Dapper;

namespace MotorBike.DataAccess;

/// <summary>
/// Generic Dapper repository for CRUD operations.
/// Column names are auto-resolved by querying INFORMATION_SCHEMA.COLUMNS.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _tableName;
    private readonly string _primaryKeyColumn;   // SQL column name (e.g. "Brand_ID")
    private readonly string _primaryKeyProperty; // C# property name (e.g. "BrandId")
    private readonly PropertyInfo[] _writableProperties;

    // Resolved mapping: C# property name → SQL column name
    private readonly Dictionary<string, string> _propertyToColumnMap;

    #region ── Table Metadata ──────────────────────────────────────────────

    private record TableMeta(string TableName, string PkColumn, string PkProperty);

    private static readonly Dictionary<Type, TableMeta> _tableMap = new()
    {
        [typeof(Models.CarBrand)]      = new("CarBrands",      "Brand_ID", "BrandId"),
        [typeof(Models.CarModel)]      = new("CarModels",      "Model_ID", "ModelId"),
        [typeof(Models.Cash)]          = new("Cash",           "Cash_ID",  "CashId"),
        [typeof(Models.City)]          = new("City",           "City_ID",  "CityId"),
        [typeof(Models.Color)]         = new("Colors",         "Color_ID", "ColorId"),
        [typeof(Models.Company)]       = new("Company",        "ID",       "Id"),
        [typeof(Models.Customer)]      = new("Customers",      "Cus_ID",   "CusId"),
        [typeof(Models.ExpGroup)]      = new("Exp_Group",      "Group_ID", "GroupId"),
        [typeof(Models.ItemCategory)]  = new("Item_Category",  "Cat_ID",   "CatId"),
        [typeof(Models.Omla)]          = new("Omla",           "Omla_ID",  "OmlaId"),
        [typeof(Models.Store)]         = new("Stores",         "Store_ID", "StoreId"),
        [typeof(Models.Supplier)]      = new("Suppliers",      "Supp_ID",  "SuppId"),
        [typeof(Models.Unit)]          = new("Units",          "Unit_ID",  "UnitId"),
        [typeof(Models.User)]          = new("Users",          "User_ID",  "UserId"),
        [typeof(Models.UserSub)]       = new("User_Sub",       "IDSub",    "Idsub"),
    };

    #endregion

    public Repository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;

        if (!_tableMap.TryGetValue(typeof(T), out var meta))
            throw new InvalidOperationException($"No table mapping registered for type '{typeof(T).Name}'.");

        _tableName = meta.TableName;
        _primaryKeyColumn = meta.PkColumn;
        _primaryKeyProperty = meta.PkProperty;

        // Load actual SQL column names from the database schema
        _propertyToColumnMap = BuildColumnMap();

        // Build the list of writable properties (exclude PK, navigation props, unmapped).
        _writableProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !string.Equals(p.Name, _primaryKeyProperty, StringComparison.OrdinalIgnoreCase))
            .Where(p => !IsNavigationOrSkipProperty(p))
            .Where(p => _propertyToColumnMap.ContainsKey(p.Name)) // only columns that exist in DB
            .ToArray();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<T>($"SELECT * FROM [{_tableName}]");
    }

    public async Task<T?> GetByIdAsync(object id)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<T>(
            $"SELECT * FROM [{_tableName}] WHERE [{_primaryKeyColumn}] = @Id",
            new { Id = id });
    }

    public async Task<int> InsertAsync(T entity)
    {
        var columns = new[] { $"[{_primaryKeyColumn}]" }
            .Concat(_writableProperties.Select(p => $"[{_propertyToColumnMap[p.Name]}]"));
        var parameters = new[] { $"@{_primaryKeyProperty}" }
            .Concat(_writableProperties.Select(p => $"@{p.Name}"));

        var sql = $"INSERT INTO [{_tableName}] ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        var setClauses = _writableProperties
            .Select(p => $"[{_propertyToColumnMap[p.Name]}] = @{p.Name}");
        var setString = string.Join(", ", setClauses);

        var sql = $"UPDATE [{_tableName}] SET {setString} WHERE [{_primaryKeyColumn}] = @{_primaryKeyProperty}";

        using var db = _connectionFactory.CreateConnection();
        var rows = await db.ExecuteAsync(sql, entity);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(object id)
    {
        var sql = $"DELETE FROM [{_tableName}] WHERE [{_primaryKeyColumn}] = @Id";

        using var db = _connectionFactory.CreateConnection();
        var rows = await db.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<int> GetNextIdAsync()
    {
        var sql = $"SELECT ISNULL(MAX([{_primaryKeyColumn}]), 0) + 1 FROM [{_tableName}]";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(sql);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Queries INFORMATION_SCHEMA.COLUMNS to build a mapping from
    /// C# property names to actual SQL column names.
    /// </summary>
    private Dictionary<string, string> BuildColumnMap()
    {
        using var db = _connectionFactory.CreateConnection();
        var sqlColumns = db.Query<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table",
            new { Table = _tableName }).ToList();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            // Try exact match first (e.g. "Active" → "Active")
            var match = sqlColumns.FirstOrDefault(c =>
                string.Equals(c, prop.Name, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                map[prop.Name] = match;
                continue;
            }

            // Try matching by removing underscores: "Brand_ID" → "BrandID" ≈ "BrandId"
            match = sqlColumns.FirstOrDefault(c =>
                string.Equals(c.Replace("_", ""), prop.Name, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                map[prop.Name] = match;
            }
        }

        return map;
    }

    private static bool IsNavigationOrSkipProperty(PropertyInfo prop)
    {
        var type = prop.PropertyType;

        // byte[] — binary/row-version fields (RowId)
        if (type == typeof(byte[]) && prop.Name == "RowId")
            return true;

        // ICollection<T> — navigation collection
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
            return true;

        // Virtual reference navigation (non-primitive, non-string, non-value-type)
        if (!type.IsValueType && type != typeof(string)
            && prop.GetGetMethod()?.IsVirtual == true)
            return true;

        return false;
    }
}
