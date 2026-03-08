using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Expense
{
    public int ExpId { get; set; }

    public string ExpName { get; set; } = null!;

    public string? Notes { get; set; }

    public int GroupId { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<ExpPayment> ExpPayments { get; set; } = new List<ExpPayment>();

    public virtual ExpGroup Group { get; set; } = null!;
}
