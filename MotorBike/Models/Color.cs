using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Color
{
    public int ColorId { get; set; }

    public string ColorName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<CusCar> CusCars { get; set; } = new List<CusCar>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
}
