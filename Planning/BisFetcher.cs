// Planning/BisFetcher.cs
// Fetches BiS gearsets from Etro or XIVGear, parses the JSON into a common BisGearset.
//
// URL patterns:
//   Etro:      https://etro.gg/gearset/{uuid}
//              API: https://etro.gg/api/gearsets/{uuid}/
//   XIVGear:   https://xivgear.app/?page=sl|{uuid}        (single set)
//              https://xivgear.app/?page=sg|{uuid}        (sheet/multi)
//              API: https://api.xivgear.app/shortlink/{uuid}
//
// Both APIs return JSON. Their shapes differ; the parsers below handle each.

using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GearGoblin.Planning;

public static class BisFetcher
{
    // Single shared HttpClient for the whole plugin lifetime
    private static readonly HttpClient s_http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    /// <summary>
    /// Result of a fetch attempt. Either Gearset is non-null (success) or
    /// Error is non-null (failure). Never both.
    /// </summary>
    public sealed record FetchResult(BisGearset? Gearset, string? Error);

    /// <summary>
    /// Fetch and parse a BiS URL. Auto-detects Etro vs XIVGear from the URL.
    /// Returns parsed gearset on success, error message on failure.
    /// </summary>
    public static async Task<FetchResult> FetchAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new FetchResult(null, "URL is empty.");

        try
        {
            if (TryParseEtroUrl(url, out var etroId))
                return await FetchEtroAsync(etroId, url, ct);

            if (TryParseXivGearUrl(url, out var xgId, out var xgIsSheet))
                return await FetchXivGearAsync(xgId, xgIsSheet, url, ct);

            return new FetchResult(null,
                "URL doesn't look like Etro or XIVGear. Expected something like " +
                "https://etro.gg/gearset/<uuid> or https://xivgear.app/?page=sl|<uuid>.");
        }
        catch (TaskCanceledException)
        {
            return new FetchResult(null, "Request timed out.");
        }
        catch (HttpRequestException e)
        {
            return new FetchResult(null, $"Network error: {e.Message}");
        }
        catch (Exception e)
        {
            return new FetchResult(null, $"Parse error: {e.Message}");
        }
    }

    // ─── Etro ──────────────────────────────────────────────────────────────

    private static readonly Regex s_etroRe =
        new(@"etro\.gg/gearset/([0-9a-fA-F\-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryParseEtroUrl(string url, out string id)
    {
        var m = s_etroRe.Match(url);
        if (m.Success)
        {
            id = m.Groups[1].Value;
            return true;
        }
        id = "";
        return false;
    }

    private static async Task<FetchResult> FetchEtroAsync(string id, string sourceUrl, CancellationToken ct)
    {
        var apiUrl = $"https://etro.gg/api/gearsets/{id}/";
        var resp = await s_http.GetAsync(apiUrl, ct);
        if (!resp.IsSuccessStatusCode)
            return new FetchResult(null, $"Etro returned {(int)resp.StatusCode}.");

        var json = await resp.Content.ReadAsStringAsync(ct);
        var parsed = EtroParser.Parse(json, sourceUrl);
        return new FetchResult(parsed, null);
    }

    // ─── XIVGear ───────────────────────────────────────────────────────────

    private static readonly Regex s_xgRe =
        new(@"xivgear\.app.*?page=(sl|sg)\|([0-9a-fA-F\-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryParseXivGearUrl(string url, out string id, out bool isSheet)
    {
        var m = s_xgRe.Match(url);
        if (m.Success)
        {
            isSheet = m.Groups[1].Value.Equals("sg", StringComparison.OrdinalIgnoreCase);
            id      = m.Groups[2].Value;
            return true;
        }
        id = "";
        isSheet = false;
        return false;
    }

    private static async Task<FetchResult> FetchXivGearAsync(string id, bool isSheet, string sourceUrl, CancellationToken ct)
    {
        var apiUrl = $"https://api.xivgear.app/shortlink/{id}";
        var resp = await s_http.GetAsync(apiUrl, ct);
        if (!resp.IsSuccessStatusCode)
            return new FetchResult(null, $"XIVGear returned {(int)resp.StatusCode}.");

        var json = await resp.Content.ReadAsStringAsync(ct);
        var parsed = XivGearParser.Parse(json, sourceUrl, isSheet);
        return new FetchResult(parsed, null);
    }
}
