using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

/// <summary>
/// تفاصيل مرتجع المبيعات — جدول ReSales_Sub
/// </summary>
public partial class ReSalesSub : ObservableObject
{
    public int Id { get; set; }

    public int SalesId { get; set; }

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
                Disc = Math.Round(Price * (value / 100.0), 2);
            }
        }
    }

    private double _total;
    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double Total
    {
        get => _total;
        set => SetProperty(ref _total, value);
    }

    public double UnitQty { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double QtyAll { get; set; }

    private void RecalcTotal()
    {
        Total = Qty * (Price - Disc);
    }

    public virtual ReSale Sales { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
