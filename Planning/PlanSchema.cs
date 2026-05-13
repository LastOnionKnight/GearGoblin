// Planning/PlanSchema.cs
//
// v0.4.7 — typed C# representation of the GG-PLAN:v1: wire format
// emitted by Tonberry Tactics. Schema mirrors the source of truth at
// `TonberryTactics/Models/ExportSchema.cs` (PlanPayloadV1, PlanMeldV1)
// so the two halves of the round-trip stay in sync.
//
// Wire format:
//
//   GG-PLAN:v1:<base64-encoded JSON of PlanPayloadV1>
//
// The prefix is stripped by GearsetImporter before base64 decode.
//
// If TT bumps the schema version, the prefix changes (GG-PLAN:v2:)
// and GearGoblin should refuse to import until a v2 schema is added
// here. Versioning by prefix means we can detect mismatch without
// even decoding the payload.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GearGoblin.Planning;

/// <summary>
/// Top-level GG-PLAN:v1: payload. Mirrors TT's
/// <c>TonberryTactics.Models.PlanPayloadV1</c>. Field names are
/// lower-camelCase on the wire (TT uses
/// <c>JsonNamingPolicy.CamelCase</c> on serialize); the
/// <see cref="JsonPropertyName"/> attributes below pin the mapping
/// so we don't depend on serializer policy on the read side.
/// </summary>
public sealed record PlanPayloadV1(
    [property: JsonPropertyName("v")]                int V,
    [property: JsonPropertyName("plugin")]           string Plugin,
    [property: JsonPropertyName("version")]          string Version,
    [property: JsonPropertyName("generatedAt")]      string GeneratedAt,
    [property: JsonPropertyName("sourceCharacter")]  PlanCharacterV1 SourceCharacter,
    [property: JsonPropertyName("melds")]            List<PlanMeldV1> Melds);

/// <summary>
/// Character context the plan was generated against. Used for
/// integrity warnings on import — if the user's current job or
/// ContentId differs from what's recorded here, we surface a warning
/// before letting them apply the plan.
/// </summary>
public sealed record PlanCharacterV1(
    [property: JsonPropertyName("job")]                  uint Job,
    [property: JsonPropertyName("jobAbbreviation")]      string JobAbbreviation,
    [property: JsonPropertyName("level")]                int Level,
    [property: JsonPropertyName("averageItemLevel")]     int AverageItemLevel);

/// <summary>
/// A single meld recommendation. Mirrors TT's
/// <c>TonberryTactics.Models.PlanMeldV1</c>.
///
/// <para>
/// <c>Piece</c> is the slot name as a string (e.g. "Earring",
/// "MainHand") — matches the equipped-gear slot identifier from
/// the GG-EXPORT:v1: side, so checklist rows can correlate against
/// the user's current equipment.
/// </para>
/// </summary>
public sealed record PlanMeldV1(
    [property: JsonPropertyName("piece")]        string Piece,
    [property: JsonPropertyName("pieceName")]    string PieceName,
    [property: JsonPropertyName("slotIndex")]    int SlotIndex,
    [property: JsonPropertyName("materiaName")]  string MateriaName,
    [property: JsonPropertyName("statName")]     string StatName,
    [property: JsonPropertyName("statValue")]    int StatValue);

/// <summary>
/// Diagnostic shape returned by the importer when a plan is parsed.
/// Carries the typed payload plus any warnings the import process
/// raised (schema version drift, job mismatch, ContentId mismatch,
/// stale gear, etc.). The Plan tab decides whether warnings are
/// surface-and-apply, surface-and-confirm, or hard-reject.
/// </summary>
public sealed record PlanImportResult(
    PlanPayloadV1? Payload,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> Warnings);
