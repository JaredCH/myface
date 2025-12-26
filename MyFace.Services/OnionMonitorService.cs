using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace MyFace.Services;

public class OnionStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private const int AttemptsPerCheck = 3;
    private const int MaxConcurrentChecks = 6;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(25);
    private static readonly string[] AllowedOnionSuffixes = new[] { ".onion", ".i2p" };

    private record struct ProbeOutcome(int Id, int Reachable, int Attempts, double? AverageLatency, string Status, DateTime CheckedAt);
    private record struct ProbeAttemptResult(bool Success, double LatencyMs);

    public OnionStatusService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("TorClient");
        // Rely on per-attempt cancellation instead of HttpClient timeout
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _httpClient.DefaultRequestVersion = HttpVersion.Version11;
        _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    public async Task EnsureSeedDataAsync()
    {
        // One-off seed: only run when the table is empty so user deletes stay deleted
        if (await _context.OnionStatuses.AnyAsync()) return;

        var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var seeds = GetSeedData();
        foreach (var seed in seeds)
        {
            var key = $"{seed.Name}||{seed.Url}";
            if (existingSet.Contains(key)) continue;

            _context.OnionStatuses.Add(new OnionStatus
            {
                Name = seed.Name,
                Description = seed.Category,
                OnionUrl = seed.Url,
                Status = "Unknown",
                LastChecked = null,
                ResponseTime = null,
                ReachableAttempts = 0,
                TotalAttempts = 0,
                AverageLatency = null,
                ClickCount = 0
            });
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task<OnionStatus> AddAsync(string name, string description, string onionUrl)
    {
        var status = new OnionStatus
        {
            Name = name,
            Description = description,
            OnionUrl = onionUrl,
            Status = "Unknown",
            LastChecked = null,
            ResponseTime = null,
            ReachableAttempts = 0,
            TotalAttempts = 0,
            AverageLatency = null,
            ClickCount = 0
        };

        _context.OnionStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task<List<OnionStatus>> GetAllAsync()
    {
        return await _context.OnionStatuses
            .OrderBy(m => m.Description)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<string?> RegisterClickAsync(int id)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return null;

        item.ClickCount++;
        await _context.SaveChangesAsync();
        return item.OnionUrl;
    }

    public async Task<List<OnionStatus>> GetTopByClicksAsync(int take = 4)
    {
        var top = await _context.OnionStatuses
            .OrderByDescending(o => o.ClickCount)
            .ThenBy(o => o.Name)
            .Take(take)
            .ToListAsync();

        if (top.Count >= take && top.Any(o => o.ClickCount > 0))
        {
            return top;
        }

        var fallbackNames = new[] { "Dread", "DIG", "Pitch (1)", "Pitch" };
        var fallbackCandidates = await _context.OnionStatuses.ToListAsync();
        var fallback = fallbackCandidates
            .Where(o => fallbackNames.Any(f => o.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(o => Array.FindIndex(fallbackNames, f => o.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Take(take)
            .ToList();

        return fallback.Any() ? fallback : top;
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.OnionStatuses.AsNoTracking().ToListAsync(cancellationToken);
        var throttle = new SemaphoreSlim(MaxConcurrentChecks);
        var results = new List<ProbeOutcome>(items.Count);

        var tasks = items.Select(async item =>
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
        });

        await Task.WhenAll(tasks);

        if (results.Count == 0)
        {
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
    }

    public async Task<bool> CheckAsync(int id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.OnionStatuses.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (snapshot == null) return false;

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

        item.Name = name;
        item.Description = description;
        item.OnionUrl = onionUrl;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OnionStatus?> GetByIdAsync(int id)
    {
        return await _context.OnionStatuses.FindAsync(id);
    }

    private async Task<ProbeOutcome?> ProbeAsync(OnionStatus item, CancellationToken cancellationToken)
    {
        var normalized = NormalizeOnionUrl(item.OnionUrl);
        var checkedAt = DateTime.UtcNow;

        if (normalized == null)
        {
            return new ProbeOutcome(item.Id, 0, AttemptsPerCheck, null, "Offline", checkedAt);
        }

        int successes = 0;
        double totalLatency = 0;

        for (int i = 0; i < AttemptsPerCheck; i++)
        {
            var attempt = await SendProbeAsync(normalized, cancellationToken);
            if (attempt.Success)
            {
                successes++;
                totalLatency += attempt.LatencyMs;
            }
        }

        double? averageLatency = successes > 0 ? totalLatency / successes : null;
        var status = successes == 0 ? "Offline" : (successes == AttemptsPerCheck ? "Online" : "DEGRADED");

        return new ProbeOutcome(item.Id, successes, AttemptsPerCheck, averageLatency, status, checkedAt);
    }

    private async Task<ProbeAttemptResult> SendProbeAsync(Uri uri, CancellationToken cancellationToken)
    {
        return await SendOnceAsync(uri, cancellationToken);
    }

    private async Task<ProbeAttemptResult> SendOnceAsync(Uri uri, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(AttemptTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            request.Headers.Pragma.ParseAdd("no-cache");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            return new ProbeAttemptResult(true, sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeAttemptResult(false, sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeAttemptResult(false, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeAttemptResult(false, sw.Elapsed.TotalMilliseconds);
        }
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

    private static IEnumerable<(string Name, string Category, string Url)> GetSeedData()
    {
        // Cryptocurrency & Financial
        yield return ("Blockchair", "Cryptocurrency & Financial", "http://blkchairbknpn73cfjhevhla7rkp4ed5gg2knctvv7it4lioy22defid.onion");
        yield return ("FairTrade", "Cryptocurrency & Financial", "http://fairfffoxrgxgi6tkcaxhxre2hpwiuf6autt75ianjkvmcn65dxxydad.onion");
        yield return ("Feather Wallet", "Cryptocurrency & Financial", "http://featherdvtpi7ckdbkb2yxjfwx3oyvr3xjz3oo4rszylfzjdg6pbm3id.onion");
        yield return ("Infinity Exchanger (1)", "Cryptocurrency & Financial", "http://exchanger.rjocosf2mfgkrlsrr5k52nb4lbldhiazajqcjtr35w2ulumlbggnmmad.onion");
        yield return ("Infinity Exchanger (2)", "Cryptocurrency & Financial", "http://exchanger.jp43npzv6hjvgovphlrvpaagoowhvy2rdz2374cmyoqi4nwvqkfp7tid.onion");
        yield return ("Infinity Exchanger (3)", "Cryptocurrency & Financial", "http://exchanger.pqpk7bpkpnmxqr6zmvk7kjtfwwusecwaiih77cmowl2trvocerjw3bad.onion");
        yield return ("Infinity Exchanger (4)", "Cryptocurrency & Financial", "http://exchanger.dhme3vnfeleniirt5nxuhpmjsfq5srp44uyq2jyihhnrxus7ibfqhiqd.onion");
        yield return ("LocalMonero.co", "Cryptocurrency & Financial", "http://nehdddktmhvqklsnkjqcbpmb63htee2iznpcbs5tgzctipxykpj6yrid.onion");
        yield return ("Monero", "Cryptocurrency & Financial", "http://monerotoruzizulg5ttgat2emf4d6fbmiea25detrmmy7erypseyteyd.onion");
        yield return ("Xchange.me", "Cryptocurrency & Financial", "http://xmxmrjoqo63c5notr2ds2t3pdpsg4ysqqe6e6uu2pycecmjs4ekzpmyd.onion");
        yield return ("monero.fail", "Cryptocurrency & Financial", "http://livk2fpdv4xjnjrbxfz2tw3ptogqacn2dwfzxbxr3srinryxrcewemid.onion");

        // Directories & Search Engines
        yield return ("DIG", "Directories & Search Engines", "http://digdig2nugjpszzmqe5ep2bk7lqfpdlyrkojsx2j6kzalnrqtwedr3id.onion");
        yield return ("DarkSearch", "Directories & Search Engines", "http://darkzqtmbdeauwq5mzcmgeeuhet42fhfjj4p5wbak3ofx2yqgecoeqyd.onion");
        yield return ("Darkfail", "Directories & Search Engines", "http://darkfailenbsdla5mal2mxn2uz66od5vtzd5qozslagrfzachha3f3id.onion");
        yield return ("Daunt", "Directories & Search Engines", "http://dauntdatakit2xi4usevwp3pajyppsgsrbzkfqyrp6ufsdwrnm6g5tqd.onion");
        yield return ("DuckDuckGo", "Directories & Search Engines", "https://duckduckgogg42xjoc72x3sjasowoarfbgcmvfimaftt6twagswzczad.onion");
        yield return ("Narcoogle", "Directories & Search Engines", "http://narcooqom5mfevbeb6gck5tg5y2g2f5grywcu7cp4b3bvsmlvph66wqd.onion");
        yield return ("Recon", "Directories & Search Engines", "http://recon222tttn4ob7ujdhbn3s4gjre7netvzybuvbq2bcqwltkiqinhad.onion");
        yield return ("Tor Taxi", "Directories & Search Engines", "http://tortaxi2dev6xjwbaydqzla77rrnth7yn2oqzjfmiuwn5h6vsk2a4syd.onion");
        yield return ("VormWeb", "Directories & Search Engines", "http://volkancfgpi4c7ghph6id2t7vcntenuly66qjt6oedwtjmyj4tkk5oqd.onion");

        // Email & Communication
        yield return ("DNMirc", "Email & Communication", "http://dnmirc4ammtmfnwtgomctcl7gdtwrkp2ymkzw6y6xelo5rqaqm3peiid.onion");
        yield return ("Facebook", "Email & Communication", "https://facebookwkhpilnemxj7asaniu7vnjjbiltxjqhye3mhbshg7kx5tfyd.onion");
        yield return ("Keybase", "Email & Communication", "http://keybase5wmilwokqirssclfnsqrjdsi7jdir5wy7y7iu3tanwmtp6oid.onion");
        yield return ("PissMail", "Email & Communication", "http://pissmaiamldg5ciulncthgzudvh5d55dismyqf6qdkx372n2b5osefid.onion");
        yield return ("ProtonMail", "Email & Communication", "http://protonmailrmez3lotccipshtkleegetolb73fuirgj7r4o4vfu7ozyd.onion");
        yield return ("Rise Up (1)", "Email & Communication", "http://vww6ybal4bd7szmgncyruucpgfkqahzddi37ktceo3ah7ngmcopnpyyd.onion");
        yield return ("Rise Up (2)", "Email & Communication", "http://5gdvpfoh6kb2iqbizb37lzk2ddzrwa47m6rpdueg2m656fovmbhoptqd.onion");
        yield return ("Skout", "Email & Communication", "http://skoutchtiq6i473shdg2ycebalkqherg2ham2suvcoto6wtns5fpjvid.onion");
        yield return ("TorBox", "Email & Communication", "http://torbox36ijlcevujx7mjb4oiusvwgvmue7jfn2cvutwa6kl6to3uyqad.onion");
        yield return ("Underworld", "Email & Communication", "http://fozdean5ayswi6jtseg2fgyysqt3dskoosmoc6gnqia4dxwxiuvg3oad.onion");
        yield return ("XMPP.is", "Email & Communication", "http://6voaf7iamjpufgwoulypzwwecsm2nu7j5jpgadav2rfqixmpl4d65kid.onion");
        yield return ("altaddress.org", "Email & Communication", "http://tp7mtouwvggdlm73vimqkuq7727a4ebrv4vf4cnk6lfg4fatxa6p2ryd.onion");
        yield return ("cock.li (1)", "Email & Communication", "http://rurcblzhmdk22kttfkel2zduhyu3r6to7knyc7wiorzrx5gw4c3lftad.onion");
        yield return ("cock.li (2)", "Email & Communication", "http://xdkriz6cn2avvcr2vks5lvvtmfojz2ohjzj4fhyuka55mvljeso2ztqd.onion");
        yield return ("morke.org", "Email & Communication", "http://6n5nbusxgyw46juqo3nt5v4zuivdbc7mzm74wlhg7arggetaui4yp4id.onion");

        // Forums & Communities
        yield return ("Cebulka", "Forums & Communities", "http://cebulka7uxchnbpvmqapg5pfos4ngaxglsktzvha7a5rigndghvadeyd.onion");
        yield return ("CryptBB", "Forums & Communities", "http://cryptbbtg65gibadeeo2awe3j7s6evg7eklserehqr4w4e2bis5tebid.onion");
        yield return ("DarkForest", "Forums & Communities", "http://dkforestseeaaq2dqz2uflmlsybvnq2irzn4ygyvu53oazyorednviid.onion");
        yield return ("Dread", "Forums & Communities", "http://dreadytofatroptsdj6io7l3xptbet6onoyno2yv7jicoxknyazubrad.onion");
        yield return ("EndChan (1)", "Forums & Communities", "http://endchancxfbnrfgauuxlztwlckytq7rgeo5v6pc2zd4nyqo3khfam4ad.onion");
        yield return ("EndChan (2)", "Forums & Communities", "http://enxx3byspwsdo446jujc52ucy2pf5urdbhqw3kbsfhlfjwmbpj5smdad.onion");
        yield return ("Exploit.in", "Forums & Communities", "http://exploitivzcm5dawzhe6c32bbylyggbjvh5dyvsvb5lkuz5ptmunkmqd.onion");
        yield return ("Germania", "Forums & Communities", "http://germania7zs27fu3gi76wlr5rd64cc2yjexyzvrbm4jufk7pibrpizad.onion");
        yield return ("NZ Forum", "Forums & Communities", "http://nzdnmfcf2z5pd3vwfyfy3jhwoubv6qnumdglspqhurqnuvr52khatdad.onion");
        yield return ("Pitch (1)", "Forums & Communities", "http://pitchzzzoot5i4cpsblu2d5poifsyixo5r4litxkukstre5lrbjakxid.onion");
        yield return ("Pitch (2)", "Forums & Communities", "http://pitchprash4aqilfr7sbmuwve3pnkpylqwxjbj2q5o4szcfeea6d27yd.onion");
        yield return ("The Secret Garden (1)", "Forums & Communities", "http://gardeni2xtbqdpn3mndvod5rzewor2rlo2g5iuyniqwd7vbyt7cwcrqd.onion");
        yield return ("The Secret Garden (2)", "Forums & Communities", "http://gardenjsprbg5fchsmofxdjsti76dd7use3v4q4z2suqfgeytjylliid.onion");
        yield return ("XSS.is", "Forums & Communities", "http://xssforumv3isucukbxhdhwz67hoa5e2voakcfkuieq4ch257vsburuid.onion");

        // Government & Agencies
        yield return ("CIA", "Government & Agencies", "http://ciadotgov4sjwlzihbbgxnqg3xiyrg7so2r2o3lt5wz5ypk4sxyjstad.onion");
        yield return ("Dutch National Police", "Government & Agencies", "http://tcecdnp2fhyxlcrjoyc2eimdjosr65hweut6y7r2u6b5y75yuvbkvfyd.onion");
        yield return ("NCIDE Task Force", "Government & Agencies", "http://ncidetfs7banpz2d7vpndev5somwoki5vwdpfty2k7javniujekit6ad.onion");

        // Information & Libraries
        yield return ("Darknet Bible", "Information & Libraries", "http://biblemeowimkh3utujmhm6oh2oeb3ubjw2lpgeq3lahrfr2l6ev6zgyd.onion");
        yield return ("Just Another Library", "Information & Libraries", "http://libraryfyuybp7oyidyya3ah5xvwgyx6weauoini7zyz555litmmumad.onion");
        yield return ("OpSec Manual", "Information & Libraries", "http://jqibjqqagao3peozxfs53tr6aecoyvctumfsc2xqniu4xgcrksal2iqd.onion");
        yield return ("Psychonaut Wiki", "Information & Libraries", "http://vvedndyt433kopnhv6vejxnut54y5752vpxshjaqmj7ftwiu6quiv2ad.onion");
        yield return ("Tech Learning Collective", "Information & Libraries", "http://lpiyu33yusoalp5kh3f4hak2so2sjjvjw5ykyvu2dulzosgvuffq6sad.onion");
        yield return ("The Drug Users Bible", "Information & Libraries", "http://drugusersbible.org");
        yield return ("xmrguide", "Information & Libraries", "http://monero3tssjogwg5rj37hiawndkwge5eio5wtgwvt32fvzu5drpmzlqd.onion");

        // Markets & Shops
        yield return ("3rdworld", "Markets & Shops", "http://3worldshxh35jvqlcnkoibw7q6wdguoxikdsf2hovxjk2afimd2r4oad.onion");
        yield return ("Amnesia", "Markets & Shops", "http://amnesia6iuqn46eyzcgymhzpom3gkqpj6yxacdpvu4mbq7sgskn6hvid.onion");
        yield return ("BeeFreeLSD", "Markets & Shops", "http://beefrei43e52vics3rca4553hep76hzbpyfcmnzlq2ies6ym3h6r5oqd.onion");
        yield return ("BlackOps (1)", "Markets & Shops", "http://blackops3zlgfuq4dg4yrtxoe57u3sxfa34kqzbooqbovutleqhf3zqd.onion");
        yield return ("BlackOps (2)", "Markets & Shops", "http://blackops4zfjqugajzrwokor34sv4sm5sf6pnegaevhgd7k7yt3rkbid.onion");
        yield return ("BlackOps (3)", "Markets & Shops", "http://blackops527cggb6ybayggx3bjt24xz32rotdugs6ikejxdiik6dyiid.onion");
        yield return ("BlackOps (4)", "Markets & Shops", "http://blackops66p7edjocooiipudvefdhupk27pi4y72iwnbbjvccky646yd.onion");
        yield return ("Clockwerk", "Markets & Shops", "http://mhanh3ymqx4lmghvcq5eyoq3mjj5qzanvt6i7yvwhytsv6xjuymg4fid.onion");
        yield return ("Cryptostamps", "Markets & Shops", "http://lgh3eosuqrrtvwx3s4nurujcqrm53ba5vqsbim5k5ntdpo33qkl7buyd.onion");
        yield return ("Dark Matter (1)", "Markets & Shops", "http://darkmat3kdxestusl437urshpsravq7oqb7t3m36u2l62vnmmldzdmid.onion");
        yield return ("Dark Matter (2)", "Markets & Shops", "http://darkmmaugjlnyv7i367vwddz4jkvy2sdlaeutb2uilgs5g3no54mb4qd.onion");
        yield return ("Dark Matter (3)", "Markets & Shops", "http://darkmmka22ckaagrnkgyx6kba2brra3ulhz3grfo4fz425nr7owcncad.onion");
        yield return ("Dark Matter (4)", "Markets & Shops", "http://darkmmnjhxn5sf3j2rz3hy36kdotf3apgfh4g6iez6cb2q2feazlsuad.onion");
        yield return ("Drug Hub (1)", "Markets & Shops", "http://drughub666py6fgnml5kmxa7fva5noppkf6wkai4fwwvzwt4rz645aqd.onion");
        yield return ("Drug Hub (2)", "Markets & Shops", "http://drughub.link");
        yield return ("GoldenTicket", "Markets & Shops", "http://goldtx2kn64bru6ozftz2kgdm6mulzjbokdh3dlmefgfbmiu7usbfnid.onion");
        yield return ("LSDexpress", "Markets & Shops", "http://lsdexpbuxfadzthov4cjsxialnf5kon4sz5oen6j6l3v2f46bfd6j5id.onion");
        yield return ("PowderPuffGirls", "Markets & Shops", "http://ppuffp3oytdydd2cpwnztrrikxo7l3fh4ode5taoa2huokleetx346ad.onion");
        yield return ("RushRush", "Markets & Shops", "http://rushrushxdfjtczgaux2tyw62nqcucwkrfboobxdg435ydvfsvag3lqd.onion");
        yield return ("Superwave", "Markets & Shops", "http://superwtavnevo53im3jjqimhgj4nj5xopf765cidji6e4dkpsveb5wyd.onion");
        yield return ("TopShellNL (1)", "Markets & Shops", "http://rfxzftj455ujrbzyqfw7dsaephyppkn6k3prbn7tlhcew43almqi2fid.onion");
        yield return ("TopShellNL (2)", "Markets & Shops", "http://c5p4khnw66o6mrjpzxoszn5jqlqeynw75s5jf6whzts2g74dd4bvqxid.onion");
        yield return ("TopShellNL (3)", "Markets & Shops", "http://zqnepw2dvsjtdjypbwurugtp3g46am55ugbx2wc2o4dsvuayamhtavqd.onion");
        yield return ("TorZon (1)", "Markets & Shops", "http://torzon4kv5swfazrziqvel2imhxcckc4otcvopiv5lnxzpqu4v4m5iyd.onion");
        yield return ("TorZon (2)", "Markets & Shops", "http://ujgkk42xmmip6h567srdlleefgb7hemc424cwzylrvxzgvsyoou6atid.onion");
        yield return ("TorZon (3)", "Markets & Shops", "http://7hlducio2a57if4vk5yt7g63cbs42dffztcmkyjjrb7bmdy5iuuyi7qd.onion");
        yield return ("TorZon (4)", "Markets & Shops", "http://fnc26zcinywhldpv5za5ocafznw4jsumy2zcuhiri6sqwmnpy2bzmuad.onion");
        yield return ("Tribe Seuss", "Markets & Shops", "http://eisrgs2wyyzaxemtaof3n2kqqxuxdx3y7r5vwfi7rukn3z7owxweznid.onion");
        yield return ("WeAreAMSTERDAM", "Markets & Shops", "http://waa2dbeditmgttutm4m64jvwirmwtirhbuupngbhheddadyojgjsttid.onion");

        // News & Leaks
        yield return ("BBC News (http)", "News & Leaks", "http://bbcnewsd73hkzno2ini43t4gblxvycyac5aw4gnv7t2rccijh7745uqd.onion");
        yield return ("BBC News (https)", "News & Leaks", "https://www.bbcnewsd73hkzno2ini43t4gblxvycyac5aw4gnv7t2rccijh7745uqd.onion");
        yield return ("DDOSecrets", "News & Leaks", "http://ddosxlvzzow7scc7egy75gpke54hgbg2frahxzaw6qq5osnzm7wistid.onion");
        yield return ("Monero Observer", "News & Leaks", "http://ttq5m3lsdhjysspvof6m72lbygclzyeelvn3wgjj7m3fr4djvbgepwyd.onion");
        yield return ("ProPublica", "News & Leaks", "http://p53lf57qovyuvwsc6xnrppyply3vtqm7l6pcobkmyqsiofyeznfu5uqd.onion");
        yield return ("The Intercept", "News & Leaks", "https://27m3p2uv7igmj6kvd4ql3cct5h3sdwrsajovkkndeufumzyfhlfev4qd.onion");
        yield return ("The New York Times", "News & Leaks", "https://www.nytimesn7cgmftshazwhfgzm37qxb44r64ytbb2dj3x62d2lljsciiyd.onion");
        yield return ("The Tor Times (onion)", "News & Leaks", "http://tortimeswqlzti2aqbjoieisne4ubyuoeiiugel2layyudcfrwln76qd.onion");
        yield return ("The Tor Times (i2p)", "News & Leaks", "http://times4ddo57aaryeze5pauerdf2wq43pu22orokjnr5r6obzf6nq.b32.i2p");

        // Privacy, Hosting & Software
        yield return ("0xacab", "Privacy, Hosting & Software", "http://wmj5kiic7b6kjplpbvwadnht2nh2qnkbnqtcv3dyvpqtz7ssbssftxid.onion");
        yield return ("CryptoStorm VPN", "Privacy, Hosting & Software", "http://stormwayszuh4juycoy4kwoww5gvcu2c4tdtpkup667pdwe4qenzwayd.onion");
        yield return ("DanWin1210", "Privacy, Hosting & Software", "http://danielas3rtn54uwmofdo3x2bsdifr47huasnmbgqzfrec5ubupvtpid.onion");
        yield return ("Eternal Hosting", "Privacy, Hosting & Software", "http://eternalcbrzpicytj4zyguygpmkjlkddxob7tptlr25cdipe5svyqoqd.onion");
        yield return ("Mullvad VPN", "Privacy, Hosting & Software", "http://o54hon2e2vj6c7m3aqqu6uyece65by3vgoxxhlqlsvkmacw6a7m7kiad.onion");
        yield return ("Njalla", "Privacy, Hosting & Software", "http://njallalafimoej5i4eg7vlnqjvmb6zhdh27qxcatdn647jtwwwui3nad.onion");
        yield return ("Qubes OS", "Privacy, Hosting & Software", "http://qubesosfasa4zl44o4tws22di6kepyzfeqv3tg4e3ztknltfxqrymdad.onion");
        yield return ("The Tor Project", "Privacy, Hosting & Software", "http://2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion");
        yield return ("Whonix", "Privacy, Hosting & Software", "http://dds6qkxpwdeubwucdiaord2xgbbeyds25rbsgr73tbfpqpt4a6vjwsyd.onion");

        // Utilities, Tools & File Sharing
        yield return ("Archive.is", "Utilities, Tools & File Sharing", "http://archiveiya74codqgiixo33q62qlrqtkgmcitqx5u2oeqnmn5bpcbiyd.onion");
        yield return ("ImageGirl", "Utilities, Tools & File Sharing", "http://apig2yathivs562p4gkgtpe4azrqlgxohopsgddrkjxkegkxdt75wqqd.onion");
        yield return ("Onion Archive", "Utilities, Tools & File Sharing", "http://x4ijfwy76n6jl7rs4qyhe6qi5rv6xyuos3kaczgjpjcajigjzk3k7wqd.onion");
        yield return ("OnionShare", "Utilities, Tools & File Sharing", "http://lldan5gahapx5k7iafb3s4ikijc4ni7gx5iywdflkba5y2ezyg6sjgyd.onion");
        yield return ("Pornhub", "Utilities, Tools & File Sharing", "http://pornhubvybmsymdol4iibwgwtkpwmeyd6luq2gxajgjzfjvotyt5zhyd.onion");
        yield return ("SimplyTranslate", "Utilities, Tools & File Sharing", "http://xxtbwyb5z5bdvy2f6l2yquu5qilgkjeewno4qfknvb3lkg3nmoklitid.onion");
        yield return ("SuprBay", "Utilities, Tools & File Sharing", "http://suprbaydvdcaynfo4dgdzgxb4zuso7rftlil5yg5kqjefnw4wq4ulcad.onion");
        yield return ("The Pirate Bay", "Utilities, Tools & File Sharing", "http://piratebayo3klnzokct3wt5yyxb2vpebbuyjl7m623iaxmqhsd52coid.onion");
        yield return ("dump.li", "Utilities, Tools & File Sharing", "http://dumpliwoard5qsrrsroni7bdiishealhky4snigbzfmzcquwo3kml4id.onion");
        yield return ("keys.openpgp.org", "Utilities, Tools & File Sharing", "http://zkaan2xfbuxia2wpf7ofnkbz6r5zdbbvxbunvp5g2iebopbfc4iqmbad.onion");
    }
}
