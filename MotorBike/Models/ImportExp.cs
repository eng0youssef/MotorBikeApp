using System;

namespace MotorBike.Models;

/// <summary>
/// مصروفات فاتورة الاستيراد — جدول Import_Exp
/// </summary>
public partial class ImportExp
{
    public int Id { get; set; }

    public int InvId { get; set; }

    public int ExpId { get; set; }

    public DateTime PayDate { get; set; }

    public double PayTotal { get; set; }

    public byte OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public int CashId { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ImportInvoice Inv { get; set; } = null!;

    public virtual ImportExpense Exp { get; set; } = null!;

    public virtual Cash Cash { get; set; } = null!;
}
