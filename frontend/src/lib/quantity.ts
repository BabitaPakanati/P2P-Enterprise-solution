const WHOLE_UNIT_STEP = 1;
const FRACTIONAL_UNIT_STEP = 0.1;
const DEFAULT_STEP = 0.01;

/** Count-based units - a partial one doesn't make sense, so the stepper moves a whole unit at a time. */
const WHOLE_UNITS = new Set([
  "EA", "EACH", "PC", "PCS", "PIECE", "PIECES", "UNIT", "UNITS", "NOS", "NO",
  "BOX", "BOXES", "CTN", "CARTON", "PACK", "PK", "SET", "SETS", "ROLL", "ROLLS",
  "PAIR", "PAIRS", "DOZEN", "DZ", "KIT", "KITS",
]);

/** Measured units - commonly ordered/received in tenths. */
const FRACTIONAL_UNITS = new Set([
  "KG", "KGS", "G", "GM", "GMS", "GRAM", "GRAMS", "KILOGRAM", "KILOGRAMS",
  "L", "LTR", "LITER", "LITERS", "LITRE", "LITRES", "ML",
  "MT", "TON", "TONS", "TONNE", "TONNES",
  "M", "CM", "MM", "METER", "METERS", "METRE", "METRES",
  "SQM", "SQFT", "HR", "HRS", "HOUR", "HOURS", "DAY", "DAYS",
]);

/**
 * The +/- step a quantity field should use for a given UOM - whole units for
 * something you count (EA), tenths for something you measure (KG, G, L, ...).
 * Falls back to hundredths for anything unrecognised, same as this app's
 * original one-size-fits-all default. Used by <QuantityInput>'s stepper
 * buttons only - the text box itself always accepts any typed value.
 */
export function quantityStep(uom: string): number {
  const key = uom.trim().toUpperCase();
  if (WHOLE_UNITS.has(key)) return WHOLE_UNIT_STEP;
  if (FRACTIONAL_UNITS.has(key)) return FRACTIONAL_UNIT_STEP;
  return DEFAULT_STEP;
}
