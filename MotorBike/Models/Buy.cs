using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Buy
{
    public int BuyId { get; set; }

    public DateTime BuyDate { get; set; }

    public int SuppId { get; set; }

    public double Total { get; set; }

    public double Disc { get; set; }

    public double DiscPer { get; set; }

    public double AddMoney { get; set; }

    public double Net { get; set; }

    public bool IsPer { get; set; }

    public double NetPer { get; set; }

    public bool IsCash { get; set; }

    public double Payed { get; set; }

    public int? CashId { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ICollection<BuySub> BuySubs { get; set; } = new List<BuySub>();

    public virtual Cash? Cash { get; set; }

    public virtual Supplier Supp { get; set; } = null!;
}
