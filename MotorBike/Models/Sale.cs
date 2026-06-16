using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Sale
{
    public int SalesId { get; set; }

    public DateTime SalesDate { get; set; }

    public int CusId { get; set; }

    public double Total { get; set; }

    public double Disc { get; set; }

    public double DiscPer { get; set; }

    public double AddMony { get; set; }

    public double Net { get; set; }
    public double MaintTotal { get; set; }
    public bool IsMaintenance { get; set; }

    public bool IsPer { get; set; }

    public double NetPer { get; set; }

    public bool IsCash { get; set; }

    /// <summary>الموتوسيكل المرتبط بالفاتورة (اختياري)</summary>
    public int? CarId { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public bool IsTax { get; set; }

    public double VatTax { get; set; }

    public double Tax { get; set; }

    public string? TaxNo { get; set; }

    public virtual Customer Cus { get; set; } = null!;

    public virtual ICollection<SalesSub> SalesSubs { get; set; } = new List<SalesSub>();

    public virtual ICollection<SalesPayment> SalesPayments { get; set; } = new List<SalesPayment>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double PaidAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double RemainingAmount { get; set; }
}
