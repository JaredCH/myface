using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class Pgpverification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Fingerprint { get; set; } = null!;

    public string ChallengeText { get; set; } = null!;

    public bool Verified { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
