using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// موردين الاستيراد — جدول Import_Suppliers
/// </summary>
public partial class ImportSupplier
{
    public int SuppId { get; set; }

    public string SuppName { get; set; } = null!;

    public string? Tel { get; set; }

    public string? Adress { get; set; }

    public string Country { get; set; } = null!;

    public double Debit { get; set; }

    public double Credit { get; set; }

    public byte OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public DateTime OpenDate { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public double? Bal { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<ImportInvoice> ImportInvoices { get; set; } = new List<ImportInvoice>();

    public virtual ICollection<ImportPayment> ImportPayments { get; set; } = new List<ImportPayment>();
}
