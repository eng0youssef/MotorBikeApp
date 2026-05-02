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

    /// <summary>السعة الاسطوانية بالسم المكعب (CC)</summary>
    public int? CC { get; set; }

    public bool IsStock { get; set; }

    /// <summary>مالك الموتوسيكل — FK → Customers.Cus_ID (NULL = ملك الوكالة/مخزن)</summary>
    public int? OwnerId { get; set; }

    /// <summary>الحالة: 1=مخزن، 2=مباع لعميل، 3=صيانة خارجية</summary>
    public byte StatusId { get; set; } = 1;

    /// <summary>هل تم شراؤه من مورد محلي (true) أم استيراد (false)</summary>
    public bool? IsLocalSupplier { get; set; }

    /// <summary>كود المورد الذي تم الشراء منه</summary>
    public int? SupplierId { get; set; }

    /// <summary>هل المصدر عميل (true) أم مورد (false)</summary>
    public bool IsFromCustomer { get; set; }

    /// <summary>كود العميل المصدر (إذا كان المصدر عميل)</summary>
    public int? SourceCustomerId { get; set; }

    /// <summary>سعر الشراء/الاستيراد</summary>
    public double PurchasePrice { get; set; }

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
