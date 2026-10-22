import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";

export type ThemePreference = "system" | "light" | "dark";
export type ResolvedTheme = "light" | "dark";

// Read by the inline script in index.html before first paint; keep the key in sync.
const storageKey = "agentgate.theme";
const darkQuery = "(prefers-color-scheme: dark)";

function storedPreference(): ThemePreference {
  try {
    const value = localStorage.getItem(storageKey);
    return value === "light" || value === "dark" ? value : "system";
  } catch {
    return "system"; // storage blocked (private window): follow the system
  }
}

function systemTheme(): ResolvedTheme {
  return typeof matchMedia === "function" && matchMedia(darkQuery).matches ? "dark" : "light";
}

interface ThemeContextValue {
  preference: ThemePreference;
  resolved: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState(storedPreference);
  const [system, setSystem] = useState(systemTheme);
  const resolved = preference === "system" ? system : preference;

  useEffect(() => {
    if (typeof matchMedia !== "function") return;
    const media = matchMedia(darkQuery);
    const onChange = () => setSystem(media.matches ? "dark" : "light");
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  useEffect(() => {
    // Switch in one frame: without this, every element with a colour transition fades between themes.
    const freeze = document.createElement("style");
    freeze.textContent = "*,*::before,*::after{transition:none!important}";
    document.head.appendChild(freeze);
    document.documentElement.classList.toggle("dark", resolved === "dark");
    void getComputedStyle(document.body).color; // apply the new colours before transitions come back
    const restore = requestAnimationFrame(() => freeze.remove());
    return () => {
      cancelAnimationFrame(restore);
      freeze.remove();
    };
  }, [resolved]);

  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next);
    try {
      if (next === "system") localStorage.removeItem(storageKey);
      else localStorage.setItem(storageKey, next);
    } catch {
      // the choice still applies for this session
    }
  }, []);

  const value = useMemo(() => ({ preference, resolved, setPreference }), [preference, resolved, setPreference]);
  return <ThemeContext value={value}>{children}</ThemeContext>;
}

export function useTheme(): ThemeContextValue {
  const context = use(ThemeContext);
  if (!context) throw new Error("useTheme must be used inside ThemeProvider");
  return context;
}
