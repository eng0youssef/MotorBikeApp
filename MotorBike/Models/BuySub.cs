using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class BuySub
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

    public double Total { get; set; }

    public double UnitQty { get; set; }

    public double QtyAll { get; set; }

    public virtual Buy Buy { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
