using System;
using System.Collections.Generic;
using MyFace.Core.Entities;

namespace MyFace.Web.Models.Admin;

public class UploadLogFilterModel
{
    public string? Status { get; set; }
    public string? Origin { get; set; }
    public bool? Blocked { get; set; }
    public string? Username { get; set; }
    public string? Threat { get; set; }
    public string? SessionId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class UploadLogsViewModel
{
    public IReadOnlyList<UploadScanLog> Entries { get; init; } = Array.Empty<UploadScanLog>();
    public UploadLogFilterModel Filter { get; init; } = new();
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
}
