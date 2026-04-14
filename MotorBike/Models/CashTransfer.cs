using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class CashTransfer
{
    public int PayId { get; set; }

    public DateTime PayDate { get; set; }

    public int CashId { get; set; }

    public int CashTo { get; set; }

    public double PayMoney { get; set; }

    /// <summary>سعر الصرف وقت التحويل (1 لو نفس العملة)</summary>
    public double ExchangeRate { get; set; } = 1;

    public double FromRate { get; set; } = 1;

    public double ToRate { get; set; } = 1;

    /// <summary>المبلغ بالعملة المحلية للخزينة الوجهة = PayMoney * ExchangeRate</summary>
    public double PayMoneyTo { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual Cash Cash { get; set; } = null!;
}
