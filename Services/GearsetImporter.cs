// Services/GearsetImporter.cs
//
// v0.4.7 — scaffolding. Mirrors GearsetExporter on the other side
// of the round-trip. Consumes a clipboard string of the form
//
//   GG-PLAN:v1:<base64>
//
// emitted by Tonberry Tactics, parses it into a typed
// PlanPayloadV1, validates schema and character context, and
// (when ready) persists the result into Configuration.JobPlans
// under PlanMode.Imported.
//
// Status as of v0.4.7 scaffold commit: method shapes locked,
// validation rules drafted, decode/persist bodies are TODOs.
// Calling Import() right now returns a "scaffolding-in-place"
// failure result rather than crashing the plugin — safe to ship
// the scaffold even before the bodies are filled in.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using GearGoblin.Planning;

namespace GearGoblin.Services;

public sealed class GearsetImporter : IGearsetImporter
{
    private const string ExpectedPrefix    = "GG-PLAN:v1:";
    private const int    ExpectedSchemaVer = 1;
    private const string ExpectedEmitter   = "TonberryTactics";

    private readonly Plugin plugin;

    public GearsetImporter(Plugin plugin)
    {
        this.plugin = plugin;
    }

    /// <summary>
    /// Top-level entry point invoked by the <c>/ttimport</c> slash command
    /// and the (future) Plan-tab "Import from clipboard" button. Reads
    /// the system clipboard via Dalamud's ImGui binding (which proxies
    /// the Windows clipboard), then routes to <see cref="ImportFromString"/>.
    ///
    /// <para>
    /// v0.6.1: the v0.4.7 scaffold had this method hardcoded to read an
    /// empty string and then immediately fail with "Clipboard is empty",
    /// regardless of what was actually on the system clipboard. That
    /// TODO was carried through v0.5.x and v0.6.0 unchanged. v0.6.1
    /// finally wires <c>ImGui.GetClipboardText()</c> in — Dalamud's
    /// ImGui clipboard backend syncs with the Windows clipboard so
    /// a normal Ctrl+C from the website populates this read correctly.
    /// </para>
    ///
    /// <para>
    /// Workaround for affected v0.6.0 users: <c>/ttimport &lt;paste the
    /// GG-PLAN:v1: string inline&gt;</c> still works because
    /// <see cref="Plugin.OnImportCommand"/> routes non-empty args to
    /// <see cref="ImportFromString"/> directly, bypassing the clipboard
    /// path entirely.
    /// </para>
    /// </summary>
    public PlanImportResult ImportFromClipboard()
    {
        string clipboard;
        try
        {
            // ImGui.GetClipboardText returns null if the clipboard contains
            // non-text data (image, file path list) or if access is blocked.
            // Coalesce to empty string so the IsNullOrWhiteSpace gate below
            // surfaces a clean error message either way.
            clipboard = ImGui.GetClipboardText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Defensive — Dalamud's clipboard backend is reliable in
            // practice, but a transient lock on the Windows clipboard
            // (some other app is holding it) can theoretically surface
            // here as an exception. Don't crash the slash command; give
            // the user the inline-arg workaround instead.
            return Failure(
                $"Couldn't read clipboard ({ex.GetType().Name}: {ex.Message}). " +
                "Workaround: paste the string after the command — " +
                "/ttimport GG-PLAN:v1:....");
        }

        if (string.IsNullOrWhiteSpace(clipboard))
        {
            return Failure("Clipboard is empty. Copy a GG-PLAN:v1: string from " +
                           "Tonberry Tactics and try again. (Or paste it inline: " +
                           "/ttimport GG-PLAN:v1:....)");
        }

        return ImportFromString(clipboard);
    }

    /// <summary>
    /// Same validation flow as <see cref="ImportFromClipboard"/>
    /// but takes the wire string directly. Used by the
    /// <c>/goblinimport &lt;string&gt;</c> slash-arg variant.
    /// </summary>
    public PlanImportResult ImportFromString(string wireString)
    {
        // ── Step 1: prefix and schema-version check ─────────────────
        if (string.IsNullOrWhiteSpace(wireString))
            return Failure("Empty input.");

        wireString = wireString.Trim();

        if (!wireString.StartsWith(ExpectedPrefix, StringComparison.Ordinal))
        {
            return Failure(
                "Doesn't look like a GG-PLAN:v1: string. " +
                "Expected prefix '" + ExpectedPrefix + "'. " +
                "Make sure you copied the plan output from Tonberry Tactics, " +
                "not the gear export from the in-game plugin.");
        }

        var encoded = wireString.Substring(ExpectedPrefix.Length);

        // ── Step 2: base64 decode ───────────────────────────────────
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            return Failure("Base64 decode failed: " + ex.Message +
                           ". String may be truncated or corrupted in transit.");
        }

        var json = Encoding.UTF8.GetString(bytes);

        // ── Step 3: JSON deserialize ────────────────────────────────
        PlanPayloadV1? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PlanPayloadV1>(json);
        }
        catch (JsonException ex)
        {
            return Failure("JSON parse failed: " + ex.Message);
        }

        if (payload is null)
            return Failure("Decoded to null. Payload may be empty.");

        // ── Step 4: schema validation ───────────────────────────────
        if (payload.V != ExpectedSchemaVer)
        {
            return Failure(
                $"Schema version mismatch. Expected v{ExpectedSchemaVer}, " +
                $"got v{payload.V}. Update GearGoblin or update the source plan.");
        }

        if (!string.Equals(payload.Plugin, ExpectedEmitter, StringComparison.Ordinal))
        {
            return Failure(
                $"Unexpected emitter '{payload.Plugin}'. " +
                $"GG-PLAN:v1: strings should be emitted by '{ExpectedEmitter}'.");
        }

        // ── Step 5: character context warnings (non-fatal) ─────────
        var warnings = new List<string>();

        // TODO(v0.4.7 build): compare payload.SourceCharacter.Job
        // against current job, and ContentId against current
        // character. Add warnings (not errors) on mismatch so the
        // Plan tab can ask the user to confirm before applying.

        // ── Step 6: persist into Configuration ─────────────────────
        // TODO(v0.4.7 build): write the JSON body into
        // Configuration.JobPlans[contentId][jobId].ImportedPlanJson,
        // set Mode = PlanMode.Imported, set ImportedAt = UtcNow,
        // reset MeldCompletion to a list of falses sized to
        // payload.Melds.Count, then call Configuration.Save().

        return new PlanImportResult(
            Payload:      payload,
            Success:      true,
            ErrorMessage: null,
            Warnings:     warnings);
    }

    private static PlanImportResult Failure(string message) =>
        new PlanImportResult(
            Payload:      null,
            Success:      false,
            ErrorMessage: message,
            Warnings:     Array.Empty<string>());
}
