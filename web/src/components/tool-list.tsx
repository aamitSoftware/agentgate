import { Checkbox } from "@/components/ui/checkbox";
import { cn } from "@/lib/utils";

/** Tool names as chips: what an agent may call, or what a manifest offers. */
export function ToolList({ tools, className }: { tools: string[]; className?: string }) {
  if (tools.length === 0) return <span className="text-muted-foreground">No tools</span>;
  return (
    <ul className={cn("flex flex-wrap gap-1", className)} aria-label="Tools">
      {tools.map((tool) => (
        <li key={tool} className="rounded-sm border bg-muted px-1.5 font-mono text-xs leading-5 text-foreground" translate="no">
          {tool}
        </li>
      ))}
    </ul>
  );
}

/** Choosing tools from a pinned manifest, as a labelled checkbox group. */
export function ToolPicker({
  legend,
  tools,
  selected,
  onChange,
}: {
  legend: string;
  tools: { name: string; description?: string }[];
  selected: string[];
  onChange: (selected: string[]) => void;
}) {
  const toggle = (name: string, checked: boolean) => onChange(checked ? [...selected, name] : selected.filter((tool) => tool !== name));
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-2 text-sm font-medium">{legend}</legend>
      {tools.map((tool) => (
        <label
          key={tool.name}
          className="flex cursor-pointer items-start gap-2.5 rounded-md border p-2.5 has-data-[state=checked]:border-primary has-data-[state=checked]:bg-accent"
        >
          <Checkbox checked={selected.includes(tool.name)} onCheckedChange={(checked) => toggle(tool.name, checked === true)} className="mt-0.5" />
          <span className="flex min-w-0 flex-col">
            <span className="font-mono text-sm" translate="no">
              {tool.name}
            </span>
            {tool.description && <span className="text-sm text-muted-foreground">{tool.description}</span>}
          </span>
        </label>
      ))}
    </fieldset>
  );
}
