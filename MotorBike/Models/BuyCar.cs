using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// فواتير شراء السيارات — جدول Buy_Car
/// </summary>
public partial class BuyCar
{
    public int BuyId { get; set; }

    public DateTime BuyDate { get; set; }

    public bool IsCash { get; set; }

    public string OwnerName { get; set; } = null!;

    public string? OwnerTel { get; set; }

    public string? OwnerKawmy { get; set; }

    public string OwnerAdress { get; set; } = null!;

    public int? CarId { get; set; }

    public int? Mileage { get; set; }

    public double Total { get; set; }

    public bool IsTax { get; set; }

    public double VatTax { get; set; }

    public double Tax { get; set; }

    public double Net { get; set; }

    public string? TaxNo { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ICollection<BuyCarPayment> BuyCarPayments { get; set; } = new List<BuyCarPayment>();
}
