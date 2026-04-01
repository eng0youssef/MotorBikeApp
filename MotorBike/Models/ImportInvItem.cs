using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

/// <summary>
/// تفاصيل أصناف فاتورة الاستيراد — جدول Import_Inv_Item
/// Total, QtyAll, CostUnit are computed columns.
/// </summary>
public partial class ImportInvItem : ObservableObject
{
    public int Id { get; set; }

    public int InvId { get; set; }

    public int StoreId { get; set; }

    public int ItemId { get; set; }

    public int UnitId { get; set; }

    private double _qty;
    public double Qty { get => _qty; set => SetProperty(ref _qty, value); }

    private double _price;
    public double Price { get => _price; set => SetProperty(ref _price, value); }

    private double _total;
    /// <summary>Computed column</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public double Total { get => _total; set => SetProperty(ref _total, value); }

    private double _totalLocal;
    [NotMapped]
    public double TotalLocal { get => _totalLocal; set => SetProperty(ref _totalLocal, value); }


    public double UnitQty { get; set; }

    private double _qtyAll;
    /// <summary>Computed column</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public double QtyAll { get => _qtyAll; set => SetProperty(ref _qtyAll, value); }

    private decimal _costPer;
    public decimal CostPer { get => _costPer; set => SetProperty(ref _costPer, value); }

    private double? _costTotal;
    public double? CostTotal { get => _costTotal; set => SetProperty(ref _costTotal, value); }

    private double _expShareLocal;
    [NotMapped]
    public double ExpShareLocal { get => _expShareLocal; set => SetProperty(ref _expShareLocal, value); }


    private double? _costUnit;
    /// <summary>Computed column</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public double? CostUnit { get => _costUnit; set => SetProperty(ref _costUnit, value); }

    public virtual ImportInvoice Inv { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
