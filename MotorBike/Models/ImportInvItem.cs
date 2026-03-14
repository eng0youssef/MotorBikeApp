namespace MotorBike.Models;

/// <summary>
/// تفاصيل أصناف فاتورة الاستيراد — جدول Import_Inv_Item
/// Total, QtyAll, CostUnit are computed columns.
/// </summary>
public partial class ImportInvItem
{
    public int Id { get; set; }

    public int InvId { get; set; }

    public int StoreId { get; set; }

    public int ItemId { get; set; }

    public int UnitId { get; set; }

    public double Qty { get; set; }

    public double Price { get; set; }

    /// <summary>Computed column</summary>
    public double Total { get; set; }

    public double UnitQty { get; set; }

    /// <summary>Computed column</summary>
    public double QtyAll { get; set; }

    public decimal CostPer { get; set; }

    public double? CostTotal { get; set; }

    /// <summary>Computed column</summary>
    public double? CostUnit { get; set; }

    public virtual ImportInvoice Inv { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
