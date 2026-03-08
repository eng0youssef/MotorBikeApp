using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Customer
{
    public int CusId { get; set; }

    public string CusName { get; set; } = null!;

    public string? Tel { get; set; }

    public string? Adress { get; set; }

    public int CityId { get; set; }

    public DateTime OpenDate { get; set; }

    public double Debit { get; set; }

    public double Credit { get; set; }

    public byte? OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public double? Bal { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual City City { get; set; } = null!;

    public virtual ICollection<CusCar> CusCars { get; set; } = new List<CusCar>();

    public virtual ICollection<CusPayment> CusPayments { get; set; } = new List<CusPayment>();

    public virtual Omla? Omla { get; set; }

    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
