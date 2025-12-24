using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class Vote
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public int? UserId { get; set; }

    public string? SessionId { get; set; }

    public int Value { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;

    public virtual User? User { get; set; }
}
