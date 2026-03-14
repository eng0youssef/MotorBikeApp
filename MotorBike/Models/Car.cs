using System;

namespace MotorBike.Models;

/// <summary>
/// السيارات — جدول Cars
/// </summary>
public partial class Car
{
    public int CarId { get; set; }

    public int ModelId { get; set; }

    public short YearNo { get; set; }

    public string ChassisNo { get; set; } = null!;

    public string MotorNo { get; set; } = null!;

    public string PlateNo { get; set; } = null!;

    public int Mileage { get; set; }

    public int ColorId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual CarModel Model { get; set; } = null!;

    public virtual Color Color { get; set; } = null!;
}
