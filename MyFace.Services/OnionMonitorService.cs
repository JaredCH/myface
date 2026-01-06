using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyFace.Services;

public class OnionStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly MonitorLogService _monitorLog;
    private const int AttemptsPerCheck = 3;
    private const int SuccessfulAttemptsForOnline = 2;
    private const int MaxConcurrentChecks = 5;
    private static readonly TimeSpan MinimalProbeTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan BrowserProbeTimeout = TimeSpan.FromSeconds(65);
    private static readonly string[] AllowedOnionSuffixes = new[] { ".onion", ".i2p" };
    private static readonly string[] BrowserUserAgents =
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:102.0) Gecko/20100101 Firefox/102.0",
        "Mozilla/5.0 (X11; Linux x86_64; rv:115.0) Gecko/20100101 Firefox/115.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 12.6; rv:91.0) Gecko/20100101 Firefox/91.0"
    };
    private static readonly string[] ChallengePhrases =
    {
        "checking your browser",
        "ddos-guard",
        "cloudflare",
        "cf-ray",
        "just a moment",
        "attention required",
        "captcha",
        "service temporarily unavailable"
    };
    private static readonly int[] ChallengeStatusCodes = { 401, 403, 406, 407, 409, 412, 418, 421, 425, 429, 430, 503, 520, 521, 522, 523, 524 };
    private static readonly Regex MultiWhitespace = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex MirrorSuffixPattern = new("\\s*\\(mirror[^)]*\\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private record struct ProbeOutcome(int Id, int Reachable, int Attempts, double? AverageLatency, string Status, DateTime CheckedAt);
    private record struct ProbeAttemptResult(bool Success, double? LatencyMs, bool WasChallenged);
    private sealed record SeedEntry(string Name, string Category, string StoredUrl, string CanonicalKey);

    public OnionStatusService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, MonitorLogService monitorLog)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("TorClient");
        _monitorLog = monitorLog;
        // Rely on per-attempt cancellation instead of HttpClient timeout
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _httpClient.DefaultRequestVersion = HttpVersion.Version11;
        _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    public async Task EnsureSeedDataAsync()
    {
        var seeds = OnionMonitorSeedData.All
            .Select(seed =>
            {
                var storedUrl = NormalizeUrlForStorage(seed.Url);
                var key = BuildUrlComparisonKey(seed.Url);
                if (string.IsNullOrWhiteSpace(storedUrl) || string.IsNullOrWhiteSpace(key))
                {
                    return null;
                }

                return new SeedEntry(seed.Name, NormalizeCategoryLabel(seed.Category), storedUrl, key);
            })
            .Where(seed => seed is not null)
            .Cast<SeedEntry>()
            .DistinctBy(seed => seed.CanonicalKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (seeds.Count == 0)
        {
            return;
        }

        var existingEntities = await _context.OnionStatuses
            .Include(o => o.Proofs)
            .ToListAsync();

        var existingByKey = new Dictionary<string, OnionStatus>(StringComparer.OrdinalIgnoreCase);
        var exactUrlIndex = new Dictionary<string, OnionStatus>(StringComparer.Ordinal);

        foreach (var entity in existingEntities)
        {
            var exact = entity.OnionUrl ?? string.Empty;
            if (!exactUrlIndex.ContainsKey(exact))
            {
                exactUrlIndex[exact] = entity;
            }

            var key = BuildUrlComparisonKey(entity.OnionUrl);
            if (string.IsNullOrWhiteSpace(key) || existingByKey.ContainsKey(key))
            {
                continue;
            }

            existingByKey[key] = entity;
        }

        foreach (var seed in seeds)
        {
            if (existingByKey.TryGetValue(seed.CanonicalKey, out var entity))
            {
                if (!string.Equals(entity.Name, seed.Name, StringComparison.Ordinal))
                {
                    entity.Name = seed.Name;
                }

                if (!string.Equals(entity.Description, seed.Category, StringComparison.Ordinal))
                {
                    entity.Description = seed.Category;
                }

                if (!string.Equals(entity.OnionUrl, seed.StoredUrl, StringComparison.Ordinal))
                {
                    if (!exactUrlIndex.TryGetValue(seed.StoredUrl, out var owner) || owner.Id == entity.Id)
                    {
                        var previousKey = entity.OnionUrl ?? string.Empty;
                        if (exactUrlIndex.TryGetValue(previousKey, out var currentOwner) && currentOwner.Id == entity.Id)
                        {
                            exactUrlIndex.Remove(previousKey);
                        }

                        entity.OnionUrl = seed.StoredUrl;
                        exactUrlIndex[seed.StoredUrl] = entity;
                    }
                }

                continue;
            }

            if (exactUrlIndex.ContainsKey(seed.StoredUrl))
            {
                // A record already exists with the exact same stored URL (possibly migrated data).
                // Skip creating a duplicate to keep the unique index satisfied.
                continue;
            }

            var status = new OnionStatus
            {
                Name = seed.Name,
                Description = seed.Category,
                OnionUrl = seed.StoredUrl,
                Status = "Unknown",
                LastChecked = null,
                ResponseTime = null,
                ReachableAttempts = 0,
                TotalAttempts = 0,
                AverageLatency = null,
                ClickCount = 0
            };

            _context.OnionStatuses.Add(status);
            existingByKey[seed.CanonicalKey] = status;
            exactUrlIndex[seed.StoredUrl] = status;
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task<OnionStatus> AddAsync(string name, string description, string onionUrl, string? pgpProof = null)
    {
        var normalizedName = NormalizeServiceName(name);
        var category = await CanonicalizeCategoryAsync(description);
        var storedUrl = NormalizeUrlForStorage(onionUrl);
        var comparisonKey = BuildUrlComparisonKey(storedUrl);

        // Admin additions are infrequent, so load once and do an in-memory comparison to catch legacy variants.
        var existingEntries = await _context.OnionStatuses
            .Include(o => o.Proofs)
            .ToListAsync();

        var existing = existingEntries
            .FirstOrDefault(o => string.Equals(BuildUrlComparisonKey(o.OnionUrl), comparisonKey, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            var updated = false;

            if (!string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(existing.Description))
            {
                existing.Description = category;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(pgpProof))
            {
                var trimmedProof = pgpProof.Trim();
                if (!existing.Proofs.Any(p => string.Equals(p.Content, trimmedProof, StringComparison.Ordinal)))
                {
                    existing.Proofs.Add(new OnionProof
                    {
                        ProofType = "pgp-signed",
                        Content = trimmedProof,
                        CreatedAt = DateTime.UtcNow
                    });
                    updated = true;
                }
            }

            if (updated)
            {
                await _context.SaveChangesAsync();
            }

            return existing;
        }

        var canonicalName = await ResolveCanonicalNameAsync(normalizedName);

        var status = new OnionStatus
        {
            Name = canonicalName,
            Description = category,
            OnionUrl = storedUrl,
            Status = "Unknown",
            LastChecked = null,
            ResponseTime = null,
            ReachableAttempts = 0,
            TotalAttempts = 0,
            AverageLatency = null,
            ClickCount = 0,
            CanonicalName = LinkNormalizationService.NormalizeToCanonical(canonicalName),
            NormalizedKey = LinkNormalizationService.GenerateNormalizedKey(canonicalName),
            IsMirror = false,
            MirrorPriority = 0
        };

        if (!string.IsNullOrWhiteSpace(pgpProof))
        {
            status.Proofs.Add(new OnionProof
            {
                ProofType = "pgp-signed",
                Content = pgpProof.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.OnionStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task<List<OnionStatus>> GetAllAsync()
    {
        return await _context.OnionStatuses
            .Include(o => o.Proofs)
            .OrderBy(m => m.Description)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<string?> RegisterClickAsync(int id)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return null;

        // If this is a mirror, increment the parent's click count instead
        if (item.IsMirror && item.ParentId.HasValue)
        {
            var parent = await _context.OnionStatuses.FindAsync(item.ParentId.Value);
            if (parent != null)
            {
                parent.ClickCount++;
            }
        }
        else
        {
            // Primary service - increment its own count
            item.ClickCount++;
        }
        
        await _context.SaveChangesAsync();
        return item.OnionUrl;
    }

    public async Task<List<OnionStatus>> GetTopByClicksAsync(int take = 4)
    {
        // Get only primary services (non-mirrors) or services without parents
        var primaryServices = await _context.OnionStatuses
            .Include(o => o.Mirrors)
            .Where(o => !o.IsMirror || o.ParentId == null)
            .OrderByDescending(o => o.ClickCount)
            .ThenBy(o => o.CanonicalName ?? o.Name)
            .Take(take)
            .ToListAsync();

        if (primaryServices.Count >= take && primaryServices.Any(o => o.ClickCount > 0))
        {
            return primaryServices;
        }

        // Fallback: find popular services by canonical name
        var fallbackNames = new[] { "Dread", "DIG", "Pitch", "Dark Matter" };
        var allServices = await _context.OnionStatuses
            .Where(o => !o.IsMirror || o.ParentId == null)
            .ToListAsync();
        
        var fallback = allServices
            .Where(o => fallbackNames.Any(f => 
                (o.CanonicalName ?? o.Name).Contains(f, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(o => Array.FindIndex(fallbackNames, f => 
                (o.CanonicalName ?? o.Name).Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Take(take)
            .ToList();

        return fallback.Any() ? fallback : primaryServices;
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _context.OnionStatuses.AsNoTracking().ToListAsync(cancellationToken);
        var plan = BuildProbePlan(snapshots);
        if (plan.Count == 0)
        {
            AppendLog("Monitor sweep skipped: no targets available.");
            return;
        }

        AppendLog($"Monitor sweep starting across {plan.Count} targets (pool size {snapshots.Count}).");

        var throttle = new SemaphoreSlim(MaxConcurrentChecks);
        var results = new List<ProbeOutcome>(plan.Count);

        var tasks = plan.Select(async item =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                var outcome = await ProbeAsync(item, cancellationToken);
                if (outcome.HasValue)
                {
                    lock (results)
                    {
                        results.Add(outcome.Value);
                    }
                }
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        if (results.Count == 0)
        {
            AppendLog("Monitor sweep yielded no probe results.");
            return;
        }

        var ids = results.Select(r => r.Id).ToList();
        var tracked = await _context.OnionStatuses.Where(o => ids.Contains(o.Id)).ToListAsync(cancellationToken);
        var byId = results.ToDictionary(r => r.Id);

        foreach (var entity in tracked)
        {
            if (!byId.TryGetValue(entity.Id, out var outcome)) continue;

            entity.ReachableAttempts = outcome.Reachable;
            entity.TotalAttempts = outcome.Attempts;
            entity.Status = outcome.Status;
            entity.AverageLatency = outcome.AverageLatency;
            entity.ResponseTime = outcome.AverageLatency;
            entity.LastChecked = outcome.CheckedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var online = results.Count(r => string.Equals(r.Status, "Online", StringComparison.OrdinalIgnoreCase));
        var degraded = results.Count(r => string.Equals(r.Status, "DEGRADED", StringComparison.OrdinalIgnoreCase));
        AppendLog($"Monitor sweep complete: {results.Count} updated, {online} online, {degraded} degraded.");
    }

    public async Task<bool> CheckAsync(int id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.OnionStatuses.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (snapshot == null)
        {
            AppendLog($"Manual check skipped: #{id} not found.");
            return false;
        }

        var label = DescribeTarget(snapshot);
        AppendLog($"Manual check queued for {label}.");

        var outcome = await ProbeAsync(snapshot, cancellationToken);
        if (!outcome.HasValue) return false;

        var entity = await _context.OnionStatuses.FindAsync(new object?[] { id }, cancellationToken);
        if (entity == null) return false;

        entity.ReachableAttempts = outcome.Value.Reachable;
        entity.TotalAttempts = outcome.Value.Attempts;
        entity.Status = outcome.Value.Status;
        entity.AverageLatency = outcome.Value.AverageLatency;
        entity.ResponseTime = outcome.Value.AverageLatency;
        entity.LastChecked = outcome.Value.CheckedAt;

        await _context.SaveChangesAsync(cancellationToken);
        AppendLog($"Manual check complete for {DescribeTarget(entity)}: {entity.Status} ({entity.ReachableAttempts}/{entity.TotalAttempts}).");
        return true;
    }

    public async Task<bool> RemoveAsync(int id)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return false;

        _context.OnionStatuses.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(int id, string name, string description, string onionUrl)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return false;

        item.Name = await ResolveCanonicalNameAsync(NormalizeServiceName(name));
        item.Description = await CanonicalizeCategoryAsync(description);
        item.OnionUrl = NormalizeUrlForStorage(onionUrl);
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OnionStatus?> GetByIdAsync(int id)
    {
        return await _context.OnionStatuses.FindAsync(id);
    }

    public async Task<OnionProof?> GetProofByIdAsync(int proofId)
    {
        return await _context.OnionProofs
            .Include(p => p.OnionStatus)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proofId);
    }

    private async Task<string> CanonicalizeCategoryAsync(string? rawCategory)
    {
        var normalized = NormalizeCategoryLabel(rawCategory);
        if (string.Equals(normalized, "Other", StringComparison.Ordinal))
        {
            return normalized;
        }

        var existing = await _context.OnionStatuses.AsNoTracking()
            .Where(o => !string.IsNullOrEmpty(o.Description))
            .Select(o => o.Description!)
            .Distinct()
            .ToListAsync();

        var match = existing.FirstOrDefault(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        var seedMatch = OnionMonitorSeedData.All
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .FirstOrDefault(c => string.Equals(NormalizeCategoryLabel(c), normalized, StringComparison.OrdinalIgnoreCase));

        return seedMatch ?? normalized;
    }

    private static string NormalizeCategoryLabel(string? value)
    {
        var normalized = NormalizeLabel(value);
        return string.IsNullOrEmpty(normalized) ? "Other" : normalized;
    }

    private static string NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return MultiWhitespace.Replace(value.Trim(), " ");
    }

    private static string NormalizeUrlForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"http://{trimmed}";
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri)
            {
                Scheme = uri.Scheme.ToLowerInvariant(),
                Host = uri.Host.ToLowerInvariant(),
                Path = string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ? string.Empty : uri.AbsolutePath,
                Query = uri.Query
            };

            if ((builder.Scheme == "http" && builder.Port == 80) || (builder.Scheme == "https" && builder.Port == 443))
            {
                builder.Port = -1;
            }

            return builder.Uri.ToString();
        }

        return trimmed;
    }

    private static string BuildUrlComparisonKey(string? value)
    {
        var normalized = NormalizeUrlForStorage(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return normalized.ToLowerInvariant();
        }

        var host = uri.Host.ToLowerInvariant();
        var scheme = uri.Scheme.ToLowerInvariant();
        var portSegment = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var path = string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ? string.Empty : uri.AbsolutePath;
        var query = uri.Query ?? string.Empty;
        var key = $"{scheme}://{host}{portSegment}{path}{query}";
        return string.IsNullOrEmpty(path) && string.IsNullOrEmpty(query)
            ? key.TrimEnd('/')
            : key;
    }

    private static string NormalizeServiceName(string? value)
    {
        var normalized = NormalizeLabel(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return MirrorSuffixPattern.Replace(normalized, string.Empty).Trim();
    }

    private async Task<string> ResolveCanonicalNameAsync(string candidateName)
    {
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return string.Empty;
        }

        var lowered = candidateName.ToLowerInvariant();

        var exactMatch = await _context.OnionStatuses
            .AsNoTracking()
            .Where(o => o.Name.ToLower() == lowered)
            .Select(o => o.Name)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(exactMatch))
        {
            return exactMatch;
        }

        var mirrorMatch = await _context.OnionStatuses
            .AsNoTracking()
            .Where(o => o.Name.ToLower().StartsWith(lowered + " (mirror"))
            .Select(o => o.Name)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(mirrorMatch)
            ? NormalizeServiceName(mirrorMatch)
            : candidateName;
    }

    private async Task<ProbeOutcome?> ProbeAsync(OnionStatus item, CancellationToken cancellationToken)
    {
        var label = DescribeTarget(item);
        var normalized = NormalizeOnionUrl(item.OnionUrl);
        var checkedAt = DateTime.UtcNow;

        if (normalized == null)
        {
            AppendLog($"Skipping {label}: invalid onion URL '{item.OnionUrl}'.");
            return new ProbeOutcome(item.Id, 0, AttemptsPerCheck, null, "Offline", checkedAt);
        }

        AppendLog($"Checking {label} -> {normalized}");

        int successes = 0;
        int challenged = 0;
        double totalLatency = 0;

        for (int i = 0; i < AttemptsPerCheck; i++)
        {
            var attempt = await SendProbeAsync(normalized, cancellationToken);
            if (attempt.Success)
            {
                successes++;
                if (attempt.LatencyMs.HasValue)
                {
                    totalLatency += attempt.LatencyMs.Value;
                }
            }
            else if (attempt.WasChallenged)
            {
                challenged++;
            }
        }

        double? averageLatency = successes > 0 ? totalLatency / successes : null;
        var status = successes >= SuccessfulAttemptsForOnline
            ? "Online"
            : successes > 0 || challenged > 0
                ? "DEGRADED"
                : "Offline";

        var latencyDescriptor = averageLatency.HasValue ? $"{averageLatency.Value:F0} ms" : "n/a";
        AppendLog($"Result {label}: {status} ({successes}/{AttemptsPerCheck}) latency {latencyDescriptor} challenges {challenged}.");

        return new ProbeOutcome(item.Id, successes, AttemptsPerCheck, averageLatency, status, checkedAt);
    }

    private async Task<ProbeAttemptResult> SendProbeAsync(Uri uri, CancellationToken cancellationToken)
    {
        var steps = new (Func<CancellationToken, Task<ProbeAttemptResult>> Step, TimeSpan Timeout)[]
        {
            (token => TryGetAsync(uri, ProbeHeaderProfile.Minimal, token), MinimalProbeTimeout),
            (token => TryGetAsync(uri, ProbeHeaderProfile.Browser, token), BrowserProbeTimeout)
        };

        var sawChallenge = false;

        foreach (var (step, timeout) in steps)
        {
            var (attempt, timedOut) = await RunStepWithTimeout(step, timeout, cancellationToken);
            if (timedOut)
            {
                continue;
            }

            if (attempt.Success)
            {
                return attempt;
            }

            if (attempt.WasChallenged)
            {
                sawChallenge = true;
            }
        }

        return new ProbeAttemptResult(false, null, sawChallenge);
    }

    private static async Task<(ProbeAttemptResult Attempt, bool TimedOut)> RunStepWithTimeout(
        Func<CancellationToken, Task<ProbeAttemptResult>> step,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        try
        {
            var attempt = await step(linked.Token);
            return (attempt, false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (new ProbeAttemptResult(false, null, false), true);
        }
    }

    private async Task<ProbeAttemptResult> TryGetAsync(Uri uri, ProbeHeaderProfile profile, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplySharedHeaders(request);
            ApplyHeaderProfile(request, profile);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token);

            sw.Stop();

            var snippet = await ReadBodySnippetAsync(response, token);

            if (IsVisitReadyStatus(response.StatusCode) && !BodyContainsChallenge(snippet))
            {
                return new ProbeAttemptResult(true, sw.Elapsed.TotalMilliseconds, false);
            }

            var challenged = IsChallengeStatus(response.StatusCode) || BodyContainsChallenge(snippet);
            return new ProbeAttemptResult(false, null, challenged);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeAttemptResult(false, null, false);
        }
        catch
        {
            sw.Stop();
            return new ProbeAttemptResult(false, null, false);
        }
    }

    private static void ApplySharedHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        request.Headers.ConnectionClose = true;
        request.Headers.Pragma.Clear();
        request.Headers.Pragma.ParseAdd("no-cache");
    }

    private static void ApplyHeaderProfile(HttpRequestMessage request, ProbeHeaderProfile profile)
    {
        request.Headers.UserAgent.Clear();

        if (profile == ProbeHeaderProfile.Minimal)
        {
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OnionMonitor/1.0; +https://myface.invalid)");
            return;
        }

        request.Headers.Referrer = new Uri("https://www.torproject.org/");
        request.Headers.UserAgent.ParseAdd(PickBrowserUserAgent());
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
    }

    private static string PickBrowserUserAgent()
    {
        var index = RandomNumberGenerator.GetInt32(BrowserUserAgents.Length);
        return BrowserUserAgents[index];
    }

    private static async Task<string> ReadBodySnippetAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var lengthHint = response.Content.Headers.ContentLength;
            var boundedLength = lengthHint.HasValue ? Math.Min(lengthHint.Value, 8192) : 4096;
            var buffer = new byte[Math.Max(1024, (int)boundedLength)];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (bytesRead <= 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool BodyContainsChallenge(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        var sample = body.Length > 4096 ? body[..4096] : body;
        var lower = sample.ToLowerInvariant();
        return ChallengePhrases.Any(phrase => lower.Contains(phrase));
    }

    private static bool IsVisitReadyStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return code >= 200 && code < 400;
    }

    private static bool IsChallengeStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return ChallengeStatusCodes.Contains(code);
    }

    private enum ProbeHeaderProfile
    {
        Minimal,
        Browser
    }


    private static Uri? NormalizeOnionUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        var host = uri.IdnHost?.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host)) return null;

        var allowed = AllowedOnionSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (!allowed) return null;

        var builder = new UriBuilder(uri)
        {
            Path = string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery
        };

        return builder.Uri;
    }

    private enum MonitorHealth
    {
        Unknown,
        Online,
        Degraded,
        Offline
    }

    private List<OnionStatus> BuildProbePlan(List<OnionStatus> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new List<OnionStatus>();
        }

        var grouped = snapshots
            .GroupBy(s => GetBaseNameKey(s.Name), StringComparer.OrdinalIgnoreCase);

        var selections = new List<OnionStatus>(snapshots.Count);

        foreach (var group in grouped)
        {
            var ordered = group
                .OrderByDescending(s => ComputePrimaryPreference(s.Name))
                .ThenBy(s => s.LastChecked ?? DateTime.MinValue)
                .ThenBy(s => s.Id)
                .ToList();

            if (ordered.Count == 0)
            {
                continue;
            }

            var primary = ordered[0];
            selections.Add(primary);

            var mirrors = ordered.Skip(1).ToList();
            if (!mirrors.Any())
            {
                continue;
            }

            var primaryHealth = GetHealth(primary.Status);
            var mirrorCount = DetermineMirrorSampleSize(primaryHealth, mirrors.Count);
            if (mirrorCount <= 0)
            {
                continue;
            }

            var chosen = PickRandomMirrors(mirrors, mirrorCount);

            selections.AddRange(chosen);
        }

        return selections;
    }

    private static int ComputePrimaryPreference(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var trimmed = name.Trim();
        var hasParen = trimmed.EndsWith(')');
        if (!hasParen) return 3;

        var start = trimmed.LastIndexOf('(');
        if (start < 0) return 2;

        var tag = trimmed.Substring(start + 1, trimmed.Length - start - 2).Trim();
        if (string.IsNullOrEmpty(tag)) return 1;

        if (tag.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("onion", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("primary", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return int.TryParse(tag, out _) ? 1 : 2;
    }

    private static string GetBaseNameKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var trimmed = name.Trim();
        var idx = trimmed.IndexOf('(');
        if (idx > 1 && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            trimmed = trimmed[..idx].Trim();
        }
        return string.IsNullOrWhiteSpace(trimmed) ? "unknown" : trimmed;
    }

    private static string DescribeTarget(OnionStatus item)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name;
        }

        return $"#{item.Id}";
    }

    private static int DetermineMirrorSampleSize(MonitorHealth primaryHealth, int availableMirrors)
    {
        if (availableMirrors <= 0)
        {
            return 0;
        }

        var desired = primaryHealth switch
        {
            MonitorHealth.Degraded => 2,
            MonitorHealth.Offline => CalculateOfflineSample(availableMirrors),
            _ => 1
        };

        return Math.Min(availableMirrors, Math.Max(1, desired));
    }

    private static int CalculateOfflineSample(int availableMirrors)
    {
        var baseCount = Math.Min(availableMirrors, 2);
        var remaining = Math.Max(availableMirrors - baseCount, 0);
        var extra = (int)Math.Round(remaining * 0.25, MidpointRounding.AwayFromZero);
        var desired = baseCount + extra;
        return Math.Min(availableMirrors, Math.Max(baseCount, desired));
    }

    private static IReadOnlyList<OnionStatus> PickRandomMirrors(IReadOnlyList<OnionStatus> mirrors, int take)
    {
        return mirrors
            .Select(m => new { Status = m, Key = RandomNumberGenerator.GetInt32(int.MaxValue) })
            .OrderBy(x => x.Key)
            .Select(x => x.Status)
            .Take(take)
            .ToList();
    }

    private static MonitorHealth GetHealth(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return MonitorHealth.Unknown;
        if (status.Equals("Online", StringComparison.OrdinalIgnoreCase)) return MonitorHealth.Online;
        if (status.Equals("DEGRADED", StringComparison.OrdinalIgnoreCase) || status.Equals("Degraded", StringComparison.OrdinalIgnoreCase)) return MonitorHealth.Degraded;
        if (status.Equals("Offline", StringComparison.OrdinalIgnoreCase)) return MonitorHealth.Offline;
        return MonitorHealth.Unknown;
    }

    private void AppendLog(string message)
    {
        _monitorLog.Append(message);
    }

}
