import { describe, expect, it } from "vitest";
import { parseActivity, SseParser } from "./sse";

describe("SseParser", () => {
  it("parses what the Hub writes, across chunk boundaries", () => {
    const parser = new SseParser();

    expect(parser.push("event: heartbeat\ndata: \n\nevent: activ")).toEqual([{ event: "heartbeat", data: "" }]);
    expect(parser.push('ity\ndata: {"id":"1"}\nid: 1\n\n')).toEqual([{ event: "activity", data: '{"id":"1"}', id: "1" }]);
  });

  it("joins multi-line data and ignores comments", () => {
    const parser = new SseParser();
    expect(parser.push(": keep-alive\r\ndata: a\r\ndata: b\r\n\r\n")).toEqual([{ event: "message", data: "a\nb" }]);
  });
});

describe("parseActivity", () => {
  it("accepts an activity row and rejects anything else", () => {
    expect(parseActivity('{"id":"1","kind":"call","occurredOn":"2026-10-22T10:00:00Z"}')?.id).toBe("1");
    expect(parseActivity('{"id":"1","kind":"other","occurredOn":"2026-10-22T10:00:00Z"}')).toBeNull();
    expect(parseActivity("{not json")).toBeNull();
    expect(parseActivity("null")).toBeNull();
  });
});
