using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// مرتجع المبيعات — جدول ReSales
/// </summary>
public partial class ReSale
{
    public int SalesId { get; set; }

    public DateTime SalesDate { get; set; }

    public int CusId { get; set; }

    public double Total { get; set; }

    public double Disc { get; set; }

    /// <summary>Computed column</summary>
    public double DiscPer { get; set; }

    public double AddMony { get; set; }

    /// <summary>Computed column</summary>
    public double Net { get; set; }

    public bool IsPer { get; set; }

    /// <summary>Computed column</summary>
    public double NetPer { get; set; }

    public bool IsCash { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<ReSalesSub> ReSalesSubs { get; set; } = new List<ReSalesSub>();

    public virtual ICollection<ReSalesPayment> ReSalesPayments { get; set; } = new List<ReSalesPayment>();

    public virtual Customer Cus { get; set; } = null!;
}
