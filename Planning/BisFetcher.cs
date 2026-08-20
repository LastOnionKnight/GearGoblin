using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GearGoblin.Planning;

/// <summary>Fetches Etro or XIVGear target sets and normalizes them into BisGearset.</summary>
public static class BisFetcher
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public sealed record FetchResult(BisGearset? Gearset, string? Error);

    public static async Task<FetchResult> FetchAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new FetchResult(null, "URL is empty.");

        try
        {
            if (TryParseEtroUrl(url, out var etroId))
                return await FetchEtroAsync(etroId, url, ct);

            if (IsXivGearUrl(url))
                return await FetchXivGearAsync(url, ct);

            return new FetchResult(null,
                "URL doesn't look like Etro or XIVGear. Expected an Etro gearset URL or a xivgear.app URL.");
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

    private static readonly Regex EtroRegex =
        new(@"etro\.gg/gearset/([0-9a-fA-F\-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryParseEtroUrl(string url, out string id)
    {
        var match = EtroRegex.Match(url);
        if (match.Success)
        {
            id = match.Groups[1].Value;
            return true;
        }

        id = string.Empty;
        return false;
    }

    private static async Task<FetchResult> FetchEtroAsync(string id, string sourceUrl, CancellationToken ct)
    {
        var apiUrl = $"https://etro.gg/api/gearsets/{id}/";
        using var response = await Http.GetAsync(apiUrl, ct);
        if (!response.IsSuccessStatusCode)
            return new FetchResult(null, $"Etro returned {(int)response.StatusCode}.");

        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = EtroParser.Parse(json, sourceUrl);
        return parsed is null
            ? new FetchResult(null, "Etro returned data but no usable gearset was found.")
            : new FetchResult(parsed, null);
    }

    private static bool IsXivGearUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals("xivgear.app", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".xivgear.app", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<FetchResult> FetchXivGearAsync(string sourceUrl, CancellationToken ct)
    {
        // Feb 2026 XIVGear API: pass the full source URL to /basedata so the
        // API owns page/shortlink/query parsing and future URL evolution.
        var apiUrl = $"https://api.xivgear.app/basedata?url={Uri.EscapeDataString(sourceUrl)}";
        using var response = await Http.GetAsync(apiUrl, ct);
        if (!response.IsSuccessStatusCode)
            return new FetchResult(null, $"XIVGear returned {(int)response.StatusCode}.");

        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = XivGearParser.Parse(json, sourceUrl);
        return parsed is null
            ? new FetchResult(null, "XIVGear returned data but no usable gearset was found.")
            : new FetchResult(parsed, null);
    }
}
