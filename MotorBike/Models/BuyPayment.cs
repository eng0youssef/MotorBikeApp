using System;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات فواتير المشتريات — جدول Buy_Payments
/// </summary>
public partial class BuyPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public double PayMoney { get; set; }

    public int CashId { get; set; }

    public string? Notes { get; set; }

    public int BuyId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual Buy Buy { get; set; } = null!;
}
