import { Search } from "lucide-react";
import { useId } from "react";
import { Input } from "@/components/ui/input";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { count } from "@/lib/format";

export interface FilterOption<T extends string> {
  value: T;
  label: string;
  count?: number;
}

/** One-of filter over a list (2–7 options): a toggle group, never a row of hand-styled buttons. */
export function SegmentFilter<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: T;
  options: FilterOption<T>[];
  onChange: (value: T) => void;
}) {
  return (
    <ToggleGroup
      type="single"
      variant="outline"
      size="sm"
      aria-label={label}
      value={value}
      onValueChange={(next) => next && onChange(next as T)}
      className="flex-wrap"
    >
      {options.map((option) => (
        <ToggleGroupItem key={option.value} value={option.value} className="gap-1.5 px-2.5">
          {option.label}
          {option.count !== undefined && <span className="text-xs text-muted-foreground tabular">{count(option.count)}</span>}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

/** Text filter for a list; the value lives in the URL like every other filter. */
export function SearchFilter({ value, placeholder, onChange }: { value: string; placeholder: string; onChange: (value: string) => void }) {
  const id = useId();
  return (
    <div className="relative w-full sm:w-64">
      <label htmlFor={id} className="sr-only">
        {placeholder}
      </label>
      <Search aria-hidden="true" className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        id={id}
        type="search"
        name="q"
        autoComplete="off"
        spellCheck={false}
        placeholder={placeholder}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-8 pl-8 text-sm"
      />
    </div>
  );
}

export function FilterBar({ children }: { children: React.ReactNode }) {
  return <div className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">{children}</div>;
}

/** Case-insensitive match of a search term against any of the given fields. */
export function matches(term: string, ...fields: (string | null | undefined)[]): boolean {
  const needle = term.trim().toLowerCase();
  return !needle || fields.some((field) => field?.toLowerCase().includes(needle));
}
