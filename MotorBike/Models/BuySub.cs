using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

public partial class BuySub : ObservableObject
{
    public int Id { get; set; }

    public int BuyId { get; set; }

    public int StoreId { get; set; }

    public int ItemId { get; set; }

    public int UnitId { get; set; }

    private double _qty;
    public double Qty
    {
        get => _qty;
        set { if (SetProperty(ref _qty, value)) RecalcTotal(); }
    }

    private double _price;
    public double Price
    {
        get => _price;
        set { if (SetProperty(ref _price, value)) RecalcTotal(); }
    }

    private double _disc;
    public double Disc
    {
        get => _disc;
        set { if (SetProperty(ref _disc, value)) RecalcTotal(); }
    }

    private double _discPer;
    public double DiscPer
    {
        get => _discPer;
        set
        {
            if (SetProperty(ref _discPer, value))
            {
                // Auto-calc Disc from DiscPer
                Disc = Math.Round(Price * (value / 100.0), 2);
            }
        }
    }

    private double _total;
    public double Total
    {
        get => _total;
        set => SetProperty(ref _total, value);
    }

    public double UnitQty { get; set; }

    public double QtyAll { get; set; }

    private void RecalcTotal()
    {
        Total = Qty * (Price - Disc);
    }

    public virtual Buy Buy { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
