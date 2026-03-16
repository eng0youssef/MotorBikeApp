using System;

namespace MotorBike.Models;

/// <summary>
/// جدول التكاليف — جدول TblCost
/// PK: ID (bigint, identity)
/// </summary>
public partial class TblCost
{
    public long Id { get; set; }

    public int? ItemId { get; set; }

    public DateTime? MyDate { get; set; }

    public short? MyType { get; set; }

    public int? MyId { get; set; }

    public decimal? OldQty { get; set; }

    public decimal? OldCost { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? OldTotalCost { get; set; }

    public decimal? AddQty { get; set; }

    public decimal? AddCost { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? AddTotalCost { get; set; }

    public decimal? OutQty { get; set; }

    public decimal? OutCost { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? OutTotalCost { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? NewQty { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? NewCost { get; set; }

    /// <summary>Computed column</summary>
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed)]
    public decimal? NewTotalCost { get; set; }
}

