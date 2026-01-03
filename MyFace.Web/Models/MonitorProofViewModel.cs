using System;

namespace MyFace.Web.Models;

public class MonitorProofViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string OnionUrl { get; set; } = string.Empty;
    public string ProofContent { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
