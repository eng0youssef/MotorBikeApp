using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// فواتير المشتريات — جدول Buy
/// DiscPer, Net, NetPer are computed columns.
/// Payed/CashID moved to Buy_Payments table.
/// </summary>
public partial class Buy
{
    public int BuyId { get; set; }

    public DateTime BuyDate { get; set; }

    public int SuppId { get; set; }

    public double Total { get; set; }

    public bool IsTax { get; set; }

    public double VatTax { get; set; }

    public double Tax { get; set; }

    public double Disc { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double DiscPer { get; set; }

    public double AddMoney { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double Net { get; set; }

    public bool IsPer { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double NetPer { get; set; }

    public bool IsCash { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ICollection<BuySub> BuySubs { get; set; } = new List<BuySub>();

    public virtual ICollection<BuyPayment> BuyPayments { get; set; } = new List<BuyPayment>();

    public virtual Supplier Supp { get; set; } = null!;
}

