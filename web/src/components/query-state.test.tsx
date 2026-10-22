import type { UseQueryResult } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/api/client";
import { QueryState } from "./query-state";

const query = (state: Partial<UseQueryResult<string[]>>) =>
  ({ isPending: false, isError: false, data: undefined, error: null, refetch: vi.fn(), ...state }) as unknown as UseQueryResult<string[]>;

const view = (state: Partial<UseQueryResult<string[]>>) => (
  <QueryState query={query(state)} empty={<p>Nothing here.</p>}>
    {(rows) => <p>{rows.length} rows</p>}
  </QueryState>
);

describe("QueryState", () => {
  it("shows a skeleton, never a number, while loading", () => {
    render(view({ isPending: true }));
    expect(screen.getByLabelText("Loading")).toHaveAttribute("aria-busy", "true");
    expect(screen.queryByText(/rows/)).toBeNull();
  });

  it("explains a failure and retries on request", () => {
    const refetch = vi.fn();
    render(view({ isError: true, error: new ApiError(500, "Hub unavailable"), refetch } as Partial<UseQueryResult<string[]>>));
    expect(screen.getByText("Couldn't load this")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(refetch).toHaveBeenCalled();
  });

  it("tells a role that cannot read it so, without a retry", () => {
    render(view({ isError: true, error: new ApiError(403, "Forbidden") }));
    expect(screen.getByText("Not available to your role")).toBeVisible();
    expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
  });

  it("renders the empty state, then the data", () => {
    const { rerender } = render(view({ data: [] }));
    expect(screen.getByText("Nothing here.")).toBeVisible();
    rerender(view({ data: ["a", "b"] }));
    expect(screen.getByText("2 rows")).toBeVisible();
  });
});
