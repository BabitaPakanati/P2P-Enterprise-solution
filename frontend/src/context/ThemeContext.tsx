import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

export type ThemePreference = "light" | "dark" | "system";

interface ThemeValue {
  preference: ThemePreference;
  setPreference: (p: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(
    () => (localStorage.getItem("p2p.theme") as ThemePreference | null) ?? "system",
  );

  useEffect(() => {
    const root = document.documentElement;
    if (preference === "system") {
      root.removeAttribute("data-theme");
    } else {
      root.setAttribute("data-theme", preference);
    }
  }, [preference]);

  const setPreference = (p: ThemePreference) => {
    if (p === "system") localStorage.removeItem("p2p.theme");
    else localStorage.setItem("p2p.theme", p);
    setPreferenceState(p);
  };

  return <ThemeContext.Provider value={{ preference, setPreference }}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within a ThemeProvider.");
  return ctx;
}
