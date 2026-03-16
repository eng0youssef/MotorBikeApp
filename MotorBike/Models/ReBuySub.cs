using System;

namespace MotorBike.Models;

/// <summary>
/// تفاصيل مرتجع المشتريات — جدول ReBuy_Sub
/// </summary>
public partial class ReBuySub
{
    public int Id { get; set; }

    public int BuyId { get; set; }

    public int StoreId { get; set; }

    public int ItemId { get; set; }

    public int UnitId { get; set; }

    public double Qty { get; set; }

    public double Price { get; set; }

    public double Disc { get; set; }

    public double DiscPer { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double Total { get; set; }

    public double UnitQty { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double QtyAll { get; set; }

    public virtual ReBuy Buy { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}

