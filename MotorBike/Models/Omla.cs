using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Omla
{
    public byte OmlaId { get; set; }

    public string OmlaName { get; set; } = null!;

    public decimal OmlaRate { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<Cash> Cashes { get; set; } = new List<Cash>();

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
