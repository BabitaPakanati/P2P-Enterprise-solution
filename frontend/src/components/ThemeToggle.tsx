import { Sun, Moon, Monitor } from "lucide-react";
import { useTheme, type ThemePreference } from "../context/ThemeContext";

const OPTIONS: { value: ThemePreference; icon: typeof Sun; label: string }[] = [
  { value: "light", icon: Sun, label: "Light theme" },
  { value: "dark", icon: Moon, label: "Dark theme" },
  { value: "system", icon: Monitor, label: "Match system theme" },
];

export function ThemeToggle() {
  const { preference, setPreference } = useTheme();
  return (
    <div className="theme-toggle" role="group" aria-label="Theme">
      {OPTIONS.map(({ value, icon: Icon, label }) => (
        <button
          key={value}
          type="button"
          className={preference === value ? "active" : ""}
          aria-pressed={preference === value}
          aria-label={label}
          title={label}
          onClick={() => setPreference(value)}
        >
          <Icon size={14} strokeWidth={2} />
        </button>
      ))}
    </div>
  );
}
