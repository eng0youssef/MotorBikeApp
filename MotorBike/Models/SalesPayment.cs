using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات فواتير المبيعات — جدول Sales_Payments
/// </summary>
public partial class SalesPayment : ObservableObject
{
    private int _payId;
    public int PayId { get => _payId; set => SetProperty(ref _payId, value); }

    private DateTime _payDate;
    public DateTime PayDate { get => _payDate; set => SetProperty(ref _payDate, value); }

    private double _payMoney;
    public double PayMoney { get => _payMoney; set => SetProperty(ref _payMoney, value); }

    private int _cashId;
    public int CashId { get => _cashId; set => SetProperty(ref _cashId, value); }

    private string? _notes;
    public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public int SalesId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual Sale Sales { get; set; } = null!;
}
