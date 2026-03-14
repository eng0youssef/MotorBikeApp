namespace MotorBike.Models;

/// <summary>
/// تفاصيل سيارات فاتورة الاستيراد — جدول Import_Inv_Car
/// </summary>
public partial class ImportInvCar
{
    public int Id { get; set; }

    public int InvId { get; set; }

    public int CarId { get; set; }

    public int Mileage { get; set; }

    public double? Total { get; set; }

    public decimal CostPer { get; set; }

    public double? CostTotal { get; set; }

    public virtual ImportInvoice Inv { get; set; } = null!;

    public virtual Car Car { get; set; } = null!;
}
