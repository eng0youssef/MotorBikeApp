using System;
using System.Collections.Generic;

namespace MotorBike.Models;

public partial class User
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string UserPass { get; set; } = null!;

    public bool Active { get; set; }

    public int? AddUser { get; set; }

    public DateTime AddDate { get; set; }

    public string AddPc { get; set; } = null!;

    public int? EditUser { get; set; }

    public DateTime? EditDate { get; set; }

    public string? EditPc { get; set; }

    public byte[]? RowId { get; set; }

    public virtual ICollection<UserSub> UserSubs { get; set; } = new List<UserSub>();
}
