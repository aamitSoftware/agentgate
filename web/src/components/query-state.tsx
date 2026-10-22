import type { UseQueryResult } from "@tanstack/react-query";
import { Link, type LinkProps } from "@tanstack/react-router";
import { CircleAlert, FileQuestion, Lock, RotateCw } from "lucide-react";
import type { ReactNode } from "react";
import { ApiError } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty";
import { Skeleton } from "@/components/ui/skeleton";

/** A failed query, explained by what the caller can do about it (console-ui skill, state matrix). */
export function ErrorState({ error, retry, back }: { error: unknown; retry?: () => void; back?: { label: string; to: LinkProps["to"] } }) {
  const status = error instanceof ApiError ? error.status : undefined;
  if (status === 403)
    return (
      <Message icon={<Lock />} title="Not available to your role">
        Your role can't view this. Ask a registry admin if you need access.
      </Message>
    );
  if (status === 404)
    return (
      <Message
        icon={<FileQuestion />}
        title="Not found"
        action={
          back && (
            <Button variant="outline" asChild>
              <Link to={back.to}>{back.label}</Link>
            </Button>
          )
        }
      >
        It may have been removed, or the link is wrong.
      </Message>
    );
  return (
    <Message
      icon={<CircleAlert />}
      title="Couldn't load this"
      action={
        retry && (
          <Button variant="outline" onClick={retry}>
            <RotateCw data-icon="inline-start" />
            Try again
          </Button>
        )
      }
    >
      {error instanceof Error ? error.message : "Something went wrong."} If it keeps failing, check that the services are running.
    </Message>
  );
}

export function Message({ icon, title, action, children }: { icon?: ReactNode; title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Empty>
      <EmptyHeader>
        {icon && <EmptyMedia variant="icon">{icon}</EmptyMedia>}
        <EmptyTitle>{title}</EmptyTitle>
        <EmptyDescription>{children}</EmptyDescription>
      </EmptyHeader>
      {action && <EmptyContent>{action}</EmptyContent>}
    </Empty>
  );
}

/** Rows of placeholder lines in the shape of a list, while it loads. */
export function SkeletonRows({ rows = 4 }: { rows?: number }) {
  return (
    <div className="flex flex-col gap-3 p-4" aria-busy="true" aria-label="Loading">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-5" style={{ width: `${90 - index * 9}%` }} />
      ))}
    </div>
  );
}

/**
 * Renders a query in every state: loading (skeleton), error (403, 404, other, with retry), empty, and data.
 * Never renders a number or an empty list before the data has arrived.
 */
export function QueryState<T>({
  query,
  skeleton = <SkeletonRows />,
  isEmpty = (data) => Array.isArray(data) && data.length === 0,
  empty,
  back,
  children,
}: {
  query: UseQueryResult<T>;
  skeleton?: ReactNode;
  isEmpty?: (data: T) => boolean;
  empty?: ReactNode;
  back?: { label: string; to: LinkProps["to"] };
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return skeleton;
  if (query.isError) return <ErrorState error={query.error} retry={() => void query.refetch()} back={back} />;
  if (empty !== undefined && isEmpty(query.data)) return empty;
  return children(query.data);
}
