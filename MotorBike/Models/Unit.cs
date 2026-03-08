using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Unit
{
    public int UnitId { get; set; }

    public string UnitName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<BuySub> BuySubs { get; set; } = new List<BuySub>();

    public virtual ICollection<SalesSub> SalesSubs { get; set; } = new List<SalesSub>();
}
