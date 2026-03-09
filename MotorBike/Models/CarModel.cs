using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class CarModel
{
    public int ModelId { get; set; }

    public string ModelName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int BrandId { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual CarBrand Brand { get; set; } = null!;

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<CusCar> CusCars { get; set; } = new List<CusCar>();
}
