using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// بنود مصروفات الاستيراد — جدول Import_Expenses
/// </summary>
public partial class ImportExpense
{
    public int ExpId { get; set; }

    public string ExpName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<ImportExp> ImportExps { get; set; } = new List<ImportExp>();
}
