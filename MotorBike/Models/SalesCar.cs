using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// فواتير بيع السيارات — جدول Sales_Car
/// </summary>
public partial class SalesCar
{
    public int SalesId { get; set; }

    public DateTime SalesDate { get; set; }

    public int CusId { get; set; }

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

    public virtual Customer Cus { get; set; } = null!;

    public virtual ICollection<SalesCarPayment> SalesCarPayments { get; set; } = new List<SalesCarPayment>();
}
