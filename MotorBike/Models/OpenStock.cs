using System;

namespace MotorBike.Models;

/// <summary>
/// رصيد أول المدة — جدول Open_Stock
/// Composite PK: (StoreID, ItemID)
/// </summary>
public partial class OpenStock
{
    public int StoreId { get; set; }

    public int ItemId { get; set; }

    public DateTime OpenDate { get; set; }

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
}

