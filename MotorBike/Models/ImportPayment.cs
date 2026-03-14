using System;

namespace MotorBike.Models;

/// <summary>
/// مدفوعات فواتير الاستيراد — جدول Import_Payments
/// </summary>
public partial class ImportPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public int SuppId { get; set; }

    public int CashId { get; set; }

    public double PayMoney { get; set; }

    public byte OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public int? InvId { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ImportSupplier Supp { get; set; } = null!;

    public virtual Cash Cash { get; set; } = null!;

    public virtual ImportInvoice? Inv { get; set; }
}
