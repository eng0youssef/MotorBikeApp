using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class Company
{
    public short Id { get; set; }

    public string? NameAr { get; set; }

    public string? NameEn { get; set; }

    public string? AdressAr { get; set; }

    public string? AdressEn { get; set; }

    public string? Tel { get; set; }

    public string? Whatsapp { get; set; }

    public string? Facebook { get; set; }

    public string? Website { get; set; }

    public string? Segl { get; set; }

    public string? Dariba { get; set; }

    public string? CurMain { get; set; }

    public string? CurSub { get; set; }

    public byte[]? Logo { get; set; }

    public string? VersionDate { get; set; }
}
