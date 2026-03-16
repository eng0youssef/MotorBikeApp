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
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double DiscPer { get; set; }

    public double AddMony { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double Net { get; set; }

    public bool IsPer { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double NetPer { get; set; }

    public bool IsCash { get; set; }

    public double Payed { get; set; }

    public int? CashId { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<ReSalesSub> ReSalesSubs { get; set; } = new List<ReSalesSub>();

    public virtual Cash? Cash { get; set; }

    public virtual Customer Cus { get; set; } = null!;
}

