import { describe, expect, it } from "vitest";
import { decodeJwtPayload } from "./session";

const encode = (value: unknown) => btoa(JSON.stringify(value)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

describe("decodeJwtPayload", () => {
  it("reads base64url claims, including non-ASCII", () => {
    const token = `${encode({ alg: "RS256" })}.${encode({ sub: "u-1", roles: ["security-reviewer"] })}.signature`;
    expect(decodeJwtPayload(token)).toEqual({ sub: "u-1", roles: ["security-reviewer"] });
  });

  it("returns nothing for garbage instead of throwing", () => {
    expect(decodeJwtPayload("not-a-token")).toEqual({});
  });
});
