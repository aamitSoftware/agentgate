import type { ReactNode } from "react";
import { Card, CardAction, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

/**
 * A titled region of a page: Card with header, optional description and actions, then its content. Tables
 * and lists sit flush against the card edges; prose and forms get padding (`padded`).
 */
export function Section({
  title,
  description,
  actions,
  padded,
  className,
  children,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  padded?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <Card className={cn("min-w-0 overflow-hidden", className)}>
      <CardHeader className="border-b">
        <CardTitle>
          <h2>{title}</h2>
        </CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
        {actions && <CardAction className="flex items-center gap-2">{actions}</CardAction>}
      </CardHeader>
      <div className={cn(padded && "p-4")}>{children}</div>
    </Card>
  );
}
