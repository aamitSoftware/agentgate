/** One line of a unified diff. */
export interface DiffLine {
  kind: "same" | "added" | "removed";
  text: string;
}

/**
 * Line diff by longest common subsequence. Manifests are small (a tool definition is tens of lines), so the
 * quadratic table is fine and keeps the output exact, which matters when the change is one hidden sentence.
 */
export function lineDiff(before: string[], after: string[]): DiffLine[] {
  const rows = before.length;
  const columns = after.length;
  const common: number[][] = Array.from({ length: rows + 1 }, () => new Array<number>(columns + 1).fill(0));
  for (let i = rows - 1; i >= 0; i--)
    for (let j = columns - 1; j >= 0; j--)
      common[i]![j] = before[i] === after[j] ? common[i + 1]![j + 1]! + 1 : Math.max(common[i + 1]![j]!, common[i]![j + 1]!);

  const lines: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < rows && j < columns) {
    if (before[i] === after[j]) {
      lines.push({ kind: "same", text: before[i]! });
      i++;
      j++;
    } else if (common[i + 1]![j]! >= common[i]![j + 1]!) {
      lines.push({ kind: "removed", text: before[i++]! });
    } else {
      lines.push({ kind: "added", text: after[j++]! });
    }
  }
  while (i < rows) lines.push({ kind: "removed", text: before[i++]! });
  while (j < columns) lines.push({ kind: "added", text: after[j++]! });
  return lines;
}

export interface ToolDiff {
  name: string;
  change: "added" | "removed" | "changed" | "unchanged";
  lines: DiffLine[];
}

type Tool = { name: string } & Record<string, unknown>;

function parseTools(toolsJson: string | null | undefined): Map<string, Tool> {
  if (!toolsJson) return new Map();
  const tools: unknown = JSON.parse(toolsJson);
  if (!Array.isArray(tools) || !tools.every((tool) => typeof tool?.name === "string"))
    throw new Error("The manifest is not a list of named tools.");
  return new Map((tools as Tool[]).map((tool) => [tool.name, tool]));
}

/** Pretty-printed so that a diff lands on the field that changed, not on one long canonical line. */
function pretty(tool: Tool | undefined): string[] {
  return tool ? JSON.stringify(tool, null, 2).split("\n") : [];
}

/**
 * What an approval would change for agents, tool by tool: the pending manifest against the pinned one.
 * A new server has no pinned manifest, so every tool shows as added. Throws if either manifest is malformed.
 */
export function diffManifests(previousJson: string | null | undefined, currentJson: string): ToolDiff[] {
  const previous = parseTools(previousJson);
  const current = parseTools(currentJson);
  const names = [...new Set([...previous.keys(), ...current.keys()])].sort();

  return names.map((name) => {
    const before = previous.get(name);
    const after = current.get(name);
    const lines = lineDiff(pretty(before), pretty(after));
    const change = !before ? "added" : !after ? "removed" : lines.some((line) => line.kind !== "same") ? "changed" : "unchanged";
    return { name, change, lines };
  });
}
