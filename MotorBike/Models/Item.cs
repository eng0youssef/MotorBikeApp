using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Item
{
    public int ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public double Price0 { get; set; }

    public double DiscPer0 { get; set; }

    public double Price1 { get; set; }

    public double DiscPer1 { get; set; }

    public double Limit { get; set; }

    public bool IsLimit { get; set; }

    public int UnitId { get; set; }

    public int Unit2 { get; set; }

    public double Unit2Qty { get; set; }

    public int CatId { get; set; }

    public string? Notes { get; set; }

    public bool IsStock { get; set; }

    public string? Bar1 { get; set; }

    public string? Bar2 { get; set; }

    public double ImpPrice { get; set; }

    public double MinPrice { get; set; }

    public double MaxPrice { get; set; }

    public bool? IsPhoto { get; set; }

    public double? AvrgCost { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<BuySub> BuySubs { get; set; } = new List<BuySub>();

    public virtual ItemCategory Cat { get; set; } = null!;

    public virtual ICollection<SalesSub> SalesSubs { get; set; } = new List<SalesSub>();
}
