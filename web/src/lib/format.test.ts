import { describe, expect, it } from "vitest";
import { plural, timeAgo } from "./format";

describe("timeAgo", () => {
  const now = Date.parse("2026-10-22T12:00:00Z");

  it("reads past times in words", () => {
    expect(timeAgo("2026-10-22T11:58:00Z", now)).toBe("2 minutes ago");
    expect(timeAgo("2026-10-21T12:00:00Z", now)).toBe("yesterday");
  });

  it("never says a moment ago is in the future", () => {
    expect(timeAgo("2026-10-22T12:00:16Z", now)).toBe("now");
  });

  it("reads future times as such (expiries)", () => {
    expect(timeAgo("2026-11-21T12:00:00Z", now)).toBe("in 30 days");
  });
});

describe("plural", () => {
  it("agrees with the count", () => {
    expect(plural(1, "finding")).toBe("1 finding");
    expect(plural(3, "finding")).toBe("3 findings");
  });
});
