import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider, useTheme, type ThemePreference } from "./theme";

function Switcher() {
  const { setPreference } = useTheme();
  return (
    <>
      {(["system", "light", "dark"] as ThemePreference[]).map((preference) => (
        <button key={preference} onClick={() => setPreference(preference)}>
          {preference}
        </button>
      ))}
    </>
  );
}

describe("ThemeProvider", () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.className = "";
    vi.stubGlobal("matchMedia", (query: string) => ({ matches: false, media: query, addEventListener: () => {}, removeEventListener: () => {} }));
  });

  it("follows the system until a theme is chosen, and remembers the choice", () => {
    render(
      <ThemeProvider>
        <Switcher />
      </ThemeProvider>,
    );
    expect(document.documentElement).not.toHaveClass("dark");

    fireEvent.click(screen.getByRole("button", { name: "dark" }));
    expect(document.documentElement).toHaveClass("dark");
    expect(localStorage.getItem("agentgate.theme")).toBe("dark");

    fireEvent.click(screen.getByRole("button", { name: "system" }));
    expect(document.documentElement).not.toHaveClass("dark");
    expect(localStorage.getItem("agentgate.theme")).toBeNull();
  });
});
