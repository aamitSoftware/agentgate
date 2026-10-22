import { describe, expect, it } from "vitest";
import { SseParser } from "./sse";

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
