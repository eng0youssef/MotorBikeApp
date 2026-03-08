using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Sale
{
    public int SalesId { get; set; }

    public DateTime SalesDate { get; set; }

    public int CusId { get; set; }

    public double Total { get; set; }

    public double Disc { get; set; }

    public double DiscPer { get; set; }

    public double AddMony { get; set; }

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

    public byte[]? RowId { get; set; }

    public virtual Cash? Cash { get; set; }

    public virtual Customer Cus { get; set; } = null!;

    public virtual ICollection<SalesSub> SalesSubs { get; set; } = new List<SalesSub>();
}
