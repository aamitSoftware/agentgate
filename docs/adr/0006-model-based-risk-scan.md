# 0006: A model-based risk scan that can only add findings

**Status:** accepted

## Context
- **Heuristics are easy to evade.** The heuristic scanner looks for English patterns (`<IMPORTANT>`, "do not tell the user", invisible characters). An attacker who paraphrases, translates or spreads an instruction across two tools gets past it.
- **A model reads intent.** It can judge what text is trying to make an agent do, which a pattern can't.
- **The input is hostile.** The manifest under review is written by the party being reviewed, and a poisoned description is exactly a prompt injection. The scanner must not be steerable by it.

## Decision
- **Two scans, combined.** When an API key is configured, `ClaudeRiskScanner` runs the heuristic scan and asks Claude (`claude-opus-5-5`, effort `high`) to review the same manifest. The risk is the highest severity across both.
- **The model can only add findings.** "No findings" from the model never lowers the heuristic verdict.
- **Every model finding is checked.** It must name a tool that is in the manifest and quote evidence that occurs in that tool's text (case- and whitespace-insensitive), or it is dropped. Its rule comes from a fixed vocabulary and is prefixed `model-`, so reviewers can tell the two scans apart.
- **The manifest is delimited as data.** It is sent in `<manifest>` tags. It is re-serialized first so every `<` is escaped, which means no text in it can close the tag. The system prompt says the content is untrusted and must be analysed, never followed.
- **Output is constrained.** A JSON schema is sent through `output_config.format`, so the reply can't contain anything but findings.
- **Failures are visible.** Server-side fallbacks (`fallbacks: "default"`) re-serve a refusal on another model. A refusal that still comes back becomes a Medium `model-scan-refused` finding. An unreachable API or a timeout becomes a Low `model-scan-unavailable` finding, so the reviewer knows the second opinion is missing.
- **Off by default.** Without a key, the heuristic scanner runs alone and nothing leaves the network.

## Consequences
- **Manifests leave the company.** Tool names, descriptions and schemas are sent to the Anthropic API; secrets don't belong in tool descriptions anyway. The key is an Aspire secret parameter, set with `dotnet user-secrets`, and is never committed.
- **Reviews take longer to open**, by the length of a model call (bounded by `TimeoutSeconds`, 120 s by default).
- **The model can still be wrong in one direction.** It can still miss things or raise false alarms. Because a quote must be real, a false alarm points at text the reviewer can see in the diff and judge.
- **SDK:** the official `Anthropic` package (Apache-2.0), recorded with the other licences in ADR 0002.
