using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class CarBrand
{
    public int BrandId { get; set; }

    public string BrandName { get; set; } = null!;

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<CarModel> CarModels { get; set; } = new List<CarModel>();
}
