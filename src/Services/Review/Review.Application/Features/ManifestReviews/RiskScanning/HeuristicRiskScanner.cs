using System.Text.Json;
using System.Text.RegularExpressions;

namespace Review.Application.Features.ManifestReviews.RiskScanning;

/// <summary>
/// Pattern rules for the tool-poisoning shapes seen in the wild: instructions aimed at the model hidden in tool
/// text, invisible characters, schemas that accept anything, and tool names that promise destructive power.
/// Deliberately explainable: every finding quotes the text that triggered it.
/// </summary>
// ponytail: English-only patterns, so a paraphrased or translated injection gets past them; the M5 model-based scanner
// is the answer to that, and this one stays as the cheap, deterministic first pass
public sealed partial class HeuristicRiskScanner : IRiskScanner
{
    private const int EvidenceLength = 120;

    // fields whose text reaches the model; everything else in a tool definition is presentation
    private static readonly string[] _textFields = ["title", "description", "inputSchema", "outputSchema", "annotations"];

    private static readonly (string Rule, Regex Pattern)[] _hiddenInstructionPatterns =
    [
        ("hidden-instructions", InstructionTagPattern()),
        ("hidden-instructions", IgnorePreviousPattern()),
        ("hidden-instructions", ConcealFromUserPattern()),
        ("hidden-instructions", ChainedCallPattern()),
        ("sensitive-data-access", SensitivePathPattern())
    ];

    private static readonly string[] _broadParameterNames = ["sql", "query", "command", "cmd", "code", "script", "expression", "eval"];

    private static readonly string[] _dangerousNameParts =
        ["exec", "shell", "eval", "run_command", "sudo", "delete_all", "drop", "export_all", "dump", "wipe", "truncate"];

    public Task<RiskAssessment> ScanAsync(ManifestToScan manifest, CancellationToken ct) => Task.FromResult(Scan(manifest));

    public static RiskAssessment Scan(ManifestToScan manifest)
    {
        var tools = ParseTools(manifest.ToolsJson);
        var toolNames = tools.Keys.ToHashSet(StringComparer.Ordinal);
        var findings = new List<RiskFinding>();

        foreach (var (name, tool) in tools)
        {
            ScanText(name, tool, toolNames, findings);
            ScanSchema(name, tool, findings);
            ScanName(name, findings);
        }

        if (manifest.PreviousToolsJson is not null)
            ScanChanges(tools, ParseTools(manifest.PreviousToolsJson), findings);

        return RiskAssessment.From(findings);
    }

    private static void ScanText(string toolName, JsonElement tool, HashSet<string> toolNames, List<RiskFinding> findings)
    {
        foreach (var text in _textFields.Where(field => tool.TryGetProperty(field, out _)).SelectMany(field => Strings(tool.GetProperty(field))))
        {
            foreach (var (rule, pattern) in _hiddenInstructionPatterns)
                if (pattern.Match(text) is { Success: true } match)
                    findings.Add(new(toolName, rule, RiskLevel.High, Evidence(text, match.Index)));

            if (InvisibleCharacterPattern().Match(text) is { Success: true } invisible)
                findings.Add(new(toolName, "invisible-characters", RiskLevel.High,
                    $"U+{char.ConvertToUtf32(text, invisible.Index):X4} near: {Evidence(InvisibleCharacterPattern().Replace(text, "�"), invisible.Index)}"));

            if (EncodedBlobPattern().Match(text) is { Success: true } blob)
                findings.Add(new(toolName, "encoded-payload", RiskLevel.Medium, Evidence(text, blob.Index)));

            // a tool's text that talks about another tool is how a poisoned tool steers the agent elsewhere
            foreach (var other in toolNames.Where(other => other != toolName && text.Contains(other, StringComparison.Ordinal)))
                findings.Add(new(toolName, "cross-tool-reference", RiskLevel.Medium, Evidence(text, text.IndexOf(other, StringComparison.Ordinal))));
        }
    }

    private static void ScanSchema(string toolName, JsonElement tool, List<RiskFinding> findings)
    {
        if (!tool.TryGetProperty("inputSchema", out var schema) || schema.ValueKind != JsonValueKind.Object)
        {
            findings.Add(new(toolName, "missing-input-schema", RiskLevel.Medium, "The tool declares no input schema."));
            return;
        }

        if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.True)
            findings.Add(new(toolName, "open-schema", RiskLevel.Medium, "inputSchema allows additionalProperties: any argument is accepted."));

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in properties.EnumerateObject())
        {
            var isBroadName = _broadParameterNames.Any(part => property.Name.Contains(part, StringComparison.OrdinalIgnoreCase));
            var isFreeText = property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "string"
                && !property.Value.TryGetProperty("enum", out _) && !property.Value.TryGetProperty("pattern", out _)
                && !property.Value.TryGetProperty("maxLength", out _);
            if (isBroadName && isFreeText)
                findings.Add(new(toolName, "free-form-parameter", RiskLevel.Medium,
                    $"Parameter '{property.Name}' takes unconstrained text that is likely executed or interpreted."));
        }
    }

    private static void ScanName(string toolName, List<RiskFinding> findings)
    {
        var part = _dangerousNameParts.FirstOrDefault(part => toolName.Contains(part, StringComparison.OrdinalIgnoreCase));
        if (part is not null)
            findings.Add(new(toolName, "dangerous-capability", RiskLevel.Medium, $"The name suggests a broad or destructive capability ('{part}')."));
    }

    private static void ScanChanges(Dictionary<string, JsonElement> current, Dictionary<string, JsonElement> previous, List<RiskFinding> findings)
    {
        foreach (var added in current.Keys.Except(previous.Keys))
            findings.Add(new(added, "tool-added", RiskLevel.Low, "New tool since the approved manifest."));
        foreach (var removed in previous.Keys.Except(current.Keys))
            findings.Add(new(removed, "tool-removed", RiskLevel.Low, "Removed since the approved manifest."));
        foreach (var name in current.Keys.Intersect(previous.Keys))
            if (current[name].GetRawText() != previous[name].GetRawText()) // canonical JSON: equal text means equal tool
                findings.Add(new(name, "tool-changed", RiskLevel.Low, "Definition differs from the approved manifest."));
    }

    private static Dictionary<string, JsonElement> ParseTools(string toolsJson)
    {
        using var document = JsonDocument.Parse(toolsJson);
        return document.RootElement.EnumerateArray().ToDictionary(tool => tool.GetProperty("name").GetString()!, tool => tool.Clone(),
            StringComparer.Ordinal);
    }

    /// <summary>Every string value inside an element, property names included (a schema can hide text in either).</summary>
    private static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var text in Strings(property.Value))
                        yield return text;
                }
                break;
            case JsonValueKind.Array:
                foreach (var text in element.EnumerateArray().SelectMany(Strings))
                    yield return text;
                break;
        }
    }

    private static string Evidence(string text, int index)
    {
        var start = Math.Max(0, index - 20);
        var length = Math.Min(EvidenceLength, text.Length - start);
        return (start > 0 ? "…" : "") + text.Substring(start, length) + (start + length < text.Length ? "…" : "");
    }

    [GeneratedRegex(@"<\s*/?\s*(important|system|instructions?|secret|hidden|admin)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstructionTagPattern();

    [GeneratedRegex(@"\b(ignore|disregard|forget)\s+(all\s+|any\s+)?(previous|prior|above|earlier|other)\s+(instructions|prompts|rules)", RegexOptions.IgnoreCase)]
    private static partial Regex IgnorePreviousPattern();

    [GeneratedRegex(@"\b(do\s+not|don't|never|without)\s+(tell|telling|mention|mentioning|inform|informing|reveal|revealing|disclose|disclosing|notify|notifying)\b[^.]{0,30}\buser\b", RegexOptions.IgnoreCase)]
    private static partial Regex ConcealFromUserPattern();

    [GeneratedRegex(@"\b(before|after|first|always)\b[^.]{0,40}\b(call|invoke|use|run|execute)\s+(the\s+)?`?[a-z0-9_]+`?", RegexOptions.IgnoreCase)]
    private static partial Regex ChainedCallPattern();

    [GeneratedRegex(@"(~/\.ssh|id_rsa|\.env\b|/etc/passwd|/etc/shadow|mcp\.json|\.aws/credentials|api[_-]?key|private[_-]?key)", RegexOptions.IgnoreCase)]
    private static partial Regex SensitivePathPattern();

    // zero-width, bidi controls, and the Unicode tag block (U+E0000-E007F, as surrogate pairs)
    [GeneratedRegex(@"[​-‏‪-‮⁠-⁤⁦-⁩﻿]|\uDB40[\uDC00-\uDC7F]")]
    private static partial Regex InvisibleCharacterPattern();

    [GeneratedRegex(@"[A-Za-z0-9+/]{80,}={0,2}")]
    private static partial Regex EncodedBlobPattern();
}
