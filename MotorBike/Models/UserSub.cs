using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class UserSub
{
    public int Idsub { get; set; }

    public int UserId { get; set; }

    public int FrmId { get; set; }

    public string? Ability { get; set; }

    public virtual User User { get; set; } = null!;
}
