using System;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات مرتجع المشتريات — جدول ReBuy_Payments
/// </summary>
public partial class ReBuyPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public double PayMoney { get; set; }

    public int CashId { get; set; }

    public string? Notes { get; set; }

    public int BuyId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual ReBuy ReBuy { get; set; } = null!;
}
