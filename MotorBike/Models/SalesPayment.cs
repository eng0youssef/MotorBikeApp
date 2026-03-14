using System;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات فواتير المبيعات — جدول Sales_Payments
/// </summary>
public partial class SalesPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public double PayMoney { get; set; }

    public int CashId { get; set; }

    public string? Notes { get; set; }

    public int SalesId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual Sale Sales { get; set; } = null!;
}
