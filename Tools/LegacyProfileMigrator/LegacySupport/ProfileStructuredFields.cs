using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LegacyProfileMigrator.LegacySupport;

internal record PaymentRow
{
    public string Type { get; init; } = "custom";
    public string Label { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}

internal record ReferenceRow
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

internal static class ProfileStructuredFields
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
            // Fall back when legacy payloads are malformed JSON.
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
            // Fall back when legacy payloads are malformed JSON.
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
