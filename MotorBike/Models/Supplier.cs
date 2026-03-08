using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Supplier
{
    public int SuppId { get; set; }

    public string SuppName { get; set; } = null!;

    public string? Tel { get; set; }

    public string? Adress { get; set; }

    public DateTime OpenDate { get; set; }

    public double Debit { get; set; }

    public double Credit { get; set; }

    public int CityId { get; set; }

    public double? Bal { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<Buy> Buys { get; set; } = new List<Buy>();

    public virtual City City { get; set; } = null!;

    public virtual ICollection<SuppPayment> SuppPayments { get; set; } = new List<SuppPayment>();
}
