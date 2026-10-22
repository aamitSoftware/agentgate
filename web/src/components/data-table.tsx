import { createLink } from "@tanstack/react-router";
import { ArrowDown, ArrowUp, ChevronsUpDown } from "lucide-react";
import { useMemo, useState, type ComponentProps, type ReactNode } from "react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn } from "@/lib/utils";

export interface Column<T> {
  id: string;
  header: string;
  cell: (row: T) => ReactNode;
  /** Numbers and counts align to the end and use tabular figures. */
  numeric?: boolean;
  sortValue?: (row: T) => string | number;
  /** Holds its own controls (buttons, menus): raised above the row link so they stay clickable. */
  interactive?: boolean;
  className?: string;
}

type Sort = { id: string; direction: "ascending" | "descending" };

/**
 * Rows of records (console-ui skill): sticky header, sortable columns, numeric columns end-aligned. Make a
 * whole row clickable by rendering one `RowLink` in it; it stretches over the row and stays a real link
 * (keyboard, middle-click, open in new tab).
 */
export function DataTable<T>({
  label,
  columns,
  rows,
  rowKey,
  initialSort,
}: {
  label: string;
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  initialSort?: Sort;
}) {
  const [sort, setSort] = useState<Sort | undefined>(initialSort);
  const sorted = useMemo(() => {
    const column = sort && columns.find((candidate) => candidate.id === sort.id);
    if (!column?.sortValue || !sort) return rows;
    const value = column.sortValue;
    const direction = sort.direction === "ascending" ? 1 : -1;
    return rows.toSorted((left, right) => {
      const a = value(left);
      const b = value(right);
      return (typeof a === "number" && typeof b === "number" ? a - b : String(a).localeCompare(String(b))) * direction;
    });
  }, [rows, columns, sort]);

  const toggle = (id: string) =>
    setSort((current) => (current?.id === id && current.direction === "ascending" ? { id, direction: "descending" } : { id, direction: "ascending" }));

  return (
    <Table aria-label={label}>
      <TableHeader className="sticky top-0 z-20">
        <TableRow>
          {columns.map((column) => {
            const active = sort?.id === column.id;
            return (
              <TableHead
                key={column.id}
                aria-sort={column.sortValue ? (active ? sort.direction : "none") : undefined}
                className={cn(column.numeric && "text-right", column.className)}
              >
                {column.sortValue ? (
                  <button
                    type="button"
                    onClick={() => toggle(column.id)}
                    className={cn("inline-flex items-center gap-1 rounded-sm hover:text-foreground", column.numeric && "flex-row-reverse", active && "text-foreground")}
                  >
                    {column.header}
                    {active ? (
                      sort.direction === "ascending" ? <ArrowUp aria-hidden="true" className="size-3" /> : <ArrowDown aria-hidden="true" className="size-3" />
                    ) : (
                      <ChevronsUpDown aria-hidden="true" className="size-3 opacity-50" />
                    )}
                  </button>
                ) : (
                  column.header
                )}
              </TableHead>
            );
          })}
        </TableRow>
      </TableHeader>
      <TableBody>
        {sorted.map((row) => (
          <TableRow key={rowKey(row)} className="relative has-[a[data-row-link]:focus-visible]:bg-muted/50">
            {columns.map((column) => (
              <TableCell key={column.id} className={cn(column.numeric && "text-right tabular", column.interactive && "relative z-10", column.className)}>
                {column.cell(row)}
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// React 19: ref is a plain prop, no forwardRef (composition-patterns, react19-no-forwardref).
function StretchedAnchor({ className, children, ...props }: ComponentProps<"a">) {
  return (
    <a
      data-row-link=""
      className={cn("font-medium after:absolute after:inset-0 after:content-[''] hover:underline focus-visible:outline-none focus-visible:after:rounded-sm focus-visible:after:ring-2 focus-visible:after:ring-ring focus-visible:after:ring-inset", className)}
      {...props}
    >
      {children}
    </a>
  );
}

/** The link that makes a DataTable row clickable as a whole. One per row. */
export const RowLink = createLink(StretchedAnchor);
