using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorBike.Models;

/// <summary>
/// تفاصيل سيارات فاتورة الاستيراد — جدول Import_Inv_Car
/// </summary>
public partial class ImportInvCar : ObservableObject
{
    public int Id { get; set; }

    public int InvId { get; set; }

    public int CarId { get; set; }

    public int Mileage { get; set; }

    private double? _total;
    public double? Total { get => _total; set => SetProperty(ref _total, value); }

    private decimal _costPer;
    public decimal CostPer { get => _costPer; set => SetProperty(ref _costPer, value); }

    private double? _costTotal;
    public double? CostTotal { get => _costTotal; set => SetProperty(ref _costTotal, value); }

    private double _totalLocal;
    [NotMapped]
    public double TotalLocal { get => _totalLocal; set => SetProperty(ref _totalLocal, value); }
    
    private double _expShareLocal;
    [NotMapped]
    public double ExpShareLocal { get => _expShareLocal; set => SetProperty(ref _expShareLocal, value); }


    public virtual ImportInvoice Inv { get; set; } = null!;

    public virtual Car Car { get; set; } = null!;
}
