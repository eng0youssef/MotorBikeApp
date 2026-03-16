using System;
using System.Collections.Generic;

namespace MotorBike.Models;

/// <summary>
/// فواتير الاستيراد — جدول Import_Invoice
/// TotalCost is a computed column.
/// </summary>
public partial class ImportInvoice
{
    public int InvId { get; set; }

    public string InvName { get; set; } = null!;

    public int SuppId { get; set; }

    public DateTime InvDate { get; set; }

    public string? MadeIn { get; set; }

    public byte InvType { get; set; }

    public string? ShipPort { get; set; }

    public double InvTotal { get; set; }

    public byte OmlaId { get; set; }

    public decimal OmlaRate { get; set; }

    public double ExpTotal { get; set; }

    public double FrokOmla { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public double? TotalCost { get; set; }

    public string? Notes { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public virtual ImportSupplier Supp { get; set; } = null!;

    public virtual ICollection<ImportExp> ImportExps { get; set; } = new List<ImportExp>();

    public virtual ICollection<ImportInvCar> ImportInvCars { get; set; } = new List<ImportInvCar>();

    public virtual ICollection<ImportInvItem> ImportInvItems { get; set; } = new List<ImportInvItem>();

    public virtual ICollection<ImportPayment> ImportPayments { get; set; } = new List<ImportPayment>();
}
