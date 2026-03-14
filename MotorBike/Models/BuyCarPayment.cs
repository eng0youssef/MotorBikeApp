using System;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات فواتير شراء السيارات — جدول Buy_Car_Payments
/// </summary>
public partial class BuyCarPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public double PayMoney { get; set; }

    public int CashId { get; set; }

    public string? Notes { get; set; }

    public int BuyId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual BuyCar Buy { get; set; } = null!;
}
