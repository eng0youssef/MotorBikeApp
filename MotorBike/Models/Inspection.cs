using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Inspection
{
    public int InspId { get; set; }

    public DateTime InspDate { get; set; }

    public string Seller { get; set; } = null!;

    public string Buyer { get; set; } = null!;

    public int ModelId { get; set; }
    public int BrandId { get; set; }

    public short YearNo { get; set; }

    public string ChassisNo { get; set; } = null!;

    public string MotorNo { get; set; } = null!;

    public string PlateNo { get; set; } = null!;

    public int Mileage { get; set; }

    /// <summary>السعة الاسطوانية بالسم المكعب (CC)</summary>
    public int? CC { get; set; }

    public int ColorId { get; set; }

    public string? Notes { get; set; }

    public int Total { get; set; }

    public int CashId { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual Cash Cash { get; set; } = null!;

    public virtual Color Color { get; set; } = null!;
    public virtual CarBrand Brand { get; set; } = null!;

    public virtual CarModel Model { get; set; } = null!;

    public virtual ICollection<InspectionSub> InspectionSubs { get; set; } = new List<InspectionSub>();
}
