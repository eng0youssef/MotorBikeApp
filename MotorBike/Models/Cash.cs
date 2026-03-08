using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Cash
{
    public int CashId { get; set; }

    public string CashName { get; set; } = null!;

    /// <summary>
    /// Cash=0 | Bank=1 | Gary=2
    /// </summary>
    public byte CashType { get; set; }

    public DateTime OpenDate { get; set; }

    public double Debit { get; set; }

    public double Credit { get; set; }

    public string? Notes { get; set; }

    public double? Bal { get; set; }

    public byte? OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<Buy> Buys { get; set; } = new List<Buy>();

    public virtual ICollection<CashTransfer> CashTransfers { get; set; } = new List<CashTransfer>();

    public virtual ICollection<CusPayment> CusPayments { get; set; } = new List<CusPayment>();

    public virtual ICollection<ExpPayment> ExpPayments { get; set; } = new List<ExpPayment>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual Omla? Omla { get; set; }

    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();

    public virtual ICollection<SuppPayment> SuppPayments { get; set; } = new List<SuppPayment>();
}
