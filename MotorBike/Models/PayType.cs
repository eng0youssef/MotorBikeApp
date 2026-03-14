using System;

namespace MotorBike.Models;

/// <summary>
/// أنواع الدفع — جدول Pay_Types
/// </summary>
public partial class PayType
{
    public byte TypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }
}
