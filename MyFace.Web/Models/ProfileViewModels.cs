using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using MyFace.Core.Entities;

namespace MyFace.Web.Models;

public class EditProfileViewModel
{
    public string AboutMe { get; set; } = string.Empty;
    public string FontColor { get; set; } = "#e5e7eb";
    public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";
}

public class AddContactViewModel
{
    [Required]
    [MaxLength(50)]
    public string ServiceName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string AccountId { get; set; } = string.Empty;
}

public class AddNewsViewModel
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool ApplyTheme { get; set; }
}

public record PaymentRow
{
    public string Type { get; init; } = "custom";
    public string Label { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}

public record ReferenceRow
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public class SectionStylePostModel
{
    public string? CustomTitle { get; set; }
    public string? TitleAlignment { get; set; }
    public string? ContentAlignment { get; set; }
    public string? PanelColor { get; set; }
    public string? HeaderColor { get; set; }
    public bool PanelColorEnabled { get; set; }
    public bool HeaderColorEnabled { get; set; }
    public string? HeaderTextColor { get; set; }
    public bool HeaderTextColorEnabled { get; set; }
    public string? ContentTextColor { get; set; }
    public bool ContentTextColorEnabled { get; set; }
}

public static class ProfileStructuredFields
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<PaymentRow> ParsePayments(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<PaymentRow>();
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<PaymentRow>>(payload, SerializerOptions);
            if (rows != null && rows.Count > 0)
            {
                return rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Details))
                    .Select(NormalizePayment)
                    .ToList();
            }
        }
        catch
        {
            // Fallback to legacy plain-text serialization.
        }

        return FallbackPayments(payload);
    }

    public static IReadOnlyList<ReferenceRow> ParseReferences(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Array.Empty<ReferenceRow>();
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<ReferenceRow>>(payload, SerializerOptions);
            if (rows != null && rows.Count > 0)
            {
                return rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Label) || !string.IsNullOrWhiteSpace(r.Url) || !string.IsNullOrWhiteSpace(r.Notes))
                    .Select(NormalizeReference)
                    .ToList();
            }
        }
        catch
        {
            // Fallback to legacy plain-text serialization.
        }

        return FallbackReferences(payload);
    }

    public static string SerializePayments(IEnumerable<PaymentRow> rows)
    {
        var normalized = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Details))
            .Select(NormalizePayment)
            .ToList();

        return normalized.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    public static string SerializeReferences(IEnumerable<ReferenceRow> rows)
    {
        var normalized = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Label) || !string.IsNullOrWhiteSpace(r.Url) || !string.IsNullOrWhiteSpace(r.Notes))
            .Select(NormalizeReference)
            .ToList();

        return normalized.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    public static IReadOnlyList<PaymentRow> ComposePayments(string[]? types, string[]? labels, string[]? details, string? fallbackSerialized)
    {
        var rows = CollectRowCount(types, labels, details)
            .Select(index => new PaymentRow
            {
                Type = GetValue(types, index) ?? "custom",
                Label = GetValue(labels, index) ?? string.Empty,
                Details = GetValue(details, index) ?? string.Empty
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Details))
            .Select(NormalizePayment)
            .ToList();

        return rows.Count > 0 ? rows : ParsePayments(fallbackSerialized);
    }

    public static IReadOnlyList<ReferenceRow> ComposeReferences(string[]? labels, string[]? urls, string[]? notes, string? fallbackSerialized)
    {
        var rows = CollectRowCount(labels, urls, notes)
            .Select(index => new ReferenceRow
            {
                Label = GetValue(labels, index) ?? string.Empty,
                Url = GetValue(urls, index) ?? string.Empty,
                Notes = GetValue(notes, index) ?? string.Empty
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Label) || !string.IsNullOrWhiteSpace(r.Url) || !string.IsNullOrWhiteSpace(r.Notes))
            .Select(NormalizeReference)
            .ToList();

        return rows.Count > 0 ? rows : ParseReferences(fallbackSerialized);
    }

    private static IEnumerable<int> CollectRowCount(params string[]?[] arrays)
    {
        var rowCount = arrays.Max(a => a?.Length ?? 0);
        for (var i = 0; i < rowCount; i++)
        {
            yield return i;
        }
    }

    private static PaymentRow NormalizePayment(PaymentRow row)
    {
        var type = string.IsNullOrWhiteSpace(row.Type) ? "custom" : row.Type.Trim().ToLowerInvariant();
        var details = row.Details?.Trim() ?? string.Empty;
        var label = string.IsNullOrWhiteSpace(row.Label) ? GetDefaultPaymentLabel(type) : row.Label.Trim();
        return row with { Type = type, Label = label, Details = details };
    }

    private static ReferenceRow NormalizeReference(ReferenceRow row)
    {
        return row with
        {
            Label = row.Label?.Trim() ?? string.Empty,
            Url = row.Url?.Trim() ?? string.Empty,
            Notes = row.Notes?.Trim() ?? string.Empty
        };
    }

    private static IReadOnlyList<PaymentRow> FallbackPayments(string payload)
    {
        var result = new List<PaymentRow>();
        var lines = payload.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            var label = parts.Length > 1 ? parts[0].Trim() : "Payment";
            var detail = parts.Length > 1 ? parts[1].Trim() : line.Trim();
            if (string.IsNullOrWhiteSpace(detail))
            {
                continue;
            }
            result.Add(new PaymentRow { Type = "custom", Label = label, Details = detail });
        }
        return result;
    }

    private static IReadOnlyList<ReferenceRow> FallbackReferences(string payload)
    {
        var result = new List<ReferenceRow>();
        var lines = payload.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            var label = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            var url = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            var notes = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            result.Add(new ReferenceRow { Label = label, Url = url, Notes = notes });
        }
        return result;
    }

    private static string GetDefaultPaymentLabel(string type) => type switch
    {
        "xmr" => "Monero (XMR)",
        "btc" => "Bitcoin (BTC)",
        _ => "Payment"
    };

    private static string? GetValue(string[]? source, int index)
    {
        return source != null && index >= 0 && index < source.Length ? source[index] : null;
    }
}

public class ReviewSummaryViewModel
{
    public static readonly ReviewSummaryViewModel Empty = new();

    public int TotalReviews { get; init; }
    public double AverageScore { get; init; }
    public double PositivePercent { get; init; }
    public int PositiveReviews { get; init; }
}

public class UserReviewDisplayModel
{
    public string ReviewerUsername { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int CommunicationScore { get; init; }
    public int ShippingScore { get; init; }
    public int QualityScore { get; init; }
    public int OverallScore { get; init; }
    public string Comment { get; init; } = string.Empty;
    public bool IsViewer { get; init; }
}

public class ChatWindowMessageViewModel
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = "User";
    public DateTime CreatedAt { get; init; }
    public string Content { get; init; } = string.Empty;
}

public class ReviewFormModel
{
    [Range(1, 5)]
    public int CommunicationScore { get; set; } = 5;

    [Range(1, 5)]
    public int ShippingScore { get; set; } = 5;

    [Range(1, 5)]
    public int QualityScore { get; set; } = 5;

    [Range(1, 5)]
    public int OverallScore { get; set; } = 5;

    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;
}

public class UserReviewsPageViewModel
{
    public required User TargetUser { get; init; }
    public ReviewSummaryViewModel Summary { get; init; } = ReviewSummaryViewModel.Empty;
    public IReadOnlyList<UserReviewDisplayModel> Reviews { get; init; } = Array.Empty<UserReviewDisplayModel>();
    public ReviewFormModel Form { get; init; } = new();
    public bool CanSubmit { get; init; }
    public bool HasExistingReview { get; init; }
    public bool ViewerIsOwner { get; init; }
    public int CurrentPage { get; init; }
    public bool HasMorePages { get; init; }
}
