using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class CusPayment
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    /// <summary>
    /// ����� ����=0 | ����� ����=1 | ���=2 | �����=3
    /// </summary>
    public byte InType { get; set; }

    public int CusId { get; set; }

    public int? CashId { get; set; }

    public double PayMoney { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual Cash? Cash { get; set; }

    public virtual Customer Cus { get; set; } = null!;
}
