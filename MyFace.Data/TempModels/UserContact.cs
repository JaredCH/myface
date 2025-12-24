using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class UserContact
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string AccountId { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
