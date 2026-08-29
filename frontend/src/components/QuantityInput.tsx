import { Minus, Plus } from "lucide-react";
import { quantityStep } from "../lib/quantity";

interface QuantityInputProps {
  /** Caller owns the underlying type (number or a free-text string, matching how each form already tracks its lines) - this widget only ever hands back what was typed or stepped to. */
  value: string;
  uom: string;
  onChange: (raw: string) => void;
  min?: number;
  max?: number;
  disabled?: boolean;
}

/**
 * A quantity field with +/- buttons that step by an amount appropriate to the
 * UOM - a whole unit at a time for something you count (EA), a tenth at a
 * time for something you measure (KG, G, ...); see quantityStep. The text box
 * itself always takes a value typed directly: `step="any"` so the browser's
 * native step-mismatch validation can never silently block a submit just
 * because a typed number doesn't land on that increment's grid - only the
 * +/- buttons round to it.
 */
export function QuantityInput({ value, uom, onChange, min = 0, max, disabled }: QuantityInputProps) {
  const step = quantityStep(uom);
  const decimals = step >= 1 ? 0 : (step.toString().split(".")[1]?.length ?? 2);
  const numeric = Number(value) || 0;

  const bump = (direction: 1 | -1) => {
    let next = Number((numeric + direction * step).toFixed(decimals));
    if (next < min) next = min;
    if (max !== undefined && next > max) next = max;
    onChange(String(next));
  };

  return (
    <div className={`qty-field${disabled ? " is-disabled" : ""}`}>
      <button type="button" tabIndex={-1} disabled={disabled || numeric <= min} onClick={() => bump(-1)} aria-label="Decrease quantity">
        <Minus size={11} strokeWidth={2.5} />
      </button>
      <input
        type="number" inputMode="decimal" step="any" min={min} max={max}
        value={value} disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
      />
      <button type="button" tabIndex={-1} disabled={disabled || (max !== undefined && numeric >= max)} onClick={() => bump(1)} aria-label="Increase quantity">
        <Plus size={11} strokeWidth={2.5} />
      </button>
    </div>
  );
}
