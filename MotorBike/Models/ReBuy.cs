using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// مرتجع المشتريات — جدول ReBuy
/// </summary>
public partial class ReBuy
{
    public int BuyId { get; set; }

    public DateTime BuyDate { get; set; }

    public int SuppId { get; set; }

    public double Total { get; set; }

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
    public bool IsTax { get; set; }

    public double VatTax { get; set; }

    public double Tax { get; set; }

    public string? TaxNo { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ICollection<ReBuySub> ReBuySubs { get; set; } = new List<ReBuySub>();

    public virtual ICollection<ReBuyPayment> ReBuyPayments { get; set; } = new List<ReBuyPayment>();

    public virtual Supplier Supp { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double PaidAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double RemainingAmount { get; set; }
}

