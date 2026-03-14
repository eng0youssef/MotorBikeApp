namespace MotorBike.Models;

/// <summary>
/// أرصدة المخزون — جدول Stock
/// Composite PK: (ItemID, StoreID)
/// </summary>
public partial class Stock
{
    public int ItemId { get; set; }

    public int StoreId { get; set; }

    public double Qty { get; set; }
}
