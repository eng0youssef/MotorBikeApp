using System;

namespace MotorBike.Models;

/// <summary>
/// سيارات العملاء — جدول Cus_Cars
/// The car detail fields (Model, Year, Chassis, etc.) moved to the Cars table.
/// This table now links customers to cars.
/// </summary>
public partial class CusCar
{
    public int CusId { get; set; }

    public int CarId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual Customer Cus { get; set; } = null!;

    public virtual Car Car { get; set; } = null!;
}
