import { useEffect, useState } from "react";
import { useSession } from "../context/SessionContext";
import { listFields, type FieldDefinitionDto } from "../api/settings";

export type CustomFieldValues = Record<string, string>;

interface DynamicFieldsProps {
  entityType: "PurchaseRequisition" | "PurchaseOrder";
  values: CustomFieldValues;
  onChange: (values: CustomFieldValues) => void;
}

/**
 * Renders whatever extra fields this org configured for this entity type (see
 * /settings/fields), in order, respecting each field's dependency - a field with
 * DependsOnFieldKey only shows once the field it depends on currently holds
 * DependsOnValue. Mirrors (client-side, for a fast "you're missing something"
 * signal) the same rules CustomFieldValidator enforces server-side on submit - the
 * server is still the real authority, this is just not making the user wait for a
 * round trip to find out a required field is empty.
 */
export function DynamicFields({ entityType, values, onChange }: DynamicFieldsProps) {
  const { api } = useSession();
  const [fields, setFields] = useState<FieldDefinitionDto[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    listFields(api, entityType).then((all) => {
      setFields(all.filter((f) => f.isActive).sort((a, b) => a.sequence - b.sequence));
      setLoaded(true);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entityType]);

  if (!loaded || fields.length === 0) return null;

  const applies = (f: FieldDefinitionDto) => !f.dependsOnFieldKey || values[f.dependsOnFieldKey] === f.dependsOnValue;
  const visibleFields = fields.filter(applies);
  if (visibleFields.length === 0) return null;

  const setValue = (key: string, value: string) => onChange({ ...values, [key]: value });

  return (
    <div className="field-row">
      {visibleFields.map((f) => (
        <div className="field" key={f.id}>
          <label>{f.label}{f.isRequired && " *"}</label>
          {f.dataType === "Boolean" ? (
            <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", fontWeight: 400, marginTop: "0.35rem" }}>
              <input type="checkbox" style={{ width: "auto" }} checked={values[f.fieldKey] === "true"} onChange={(e) => setValue(f.fieldKey, e.target.checked ? "true" : "false")} />
              {values[f.fieldKey] === "true" ? "Yes" : "No"}
            </label>
          ) : f.dataType === "Select" ? (
            <select value={values[f.fieldKey] ?? ""} onChange={(e) => setValue(f.fieldKey, e.target.value)} required={f.isRequired}>
              <option value="">Select…</option>
              {(f.selectOptions ?? []).map((opt) => <option key={opt} value={opt}>{opt}</option>)}
            </select>
          ) : f.dataType === "Date" ? (
            <input type="date" value={values[f.fieldKey] ?? ""} onChange={(e) => setValue(f.fieldKey, e.target.value)} required={f.isRequired} />
          ) : f.dataType === "Number" ? (
            <input type="number" value={values[f.fieldKey] ?? ""} onChange={(e) => setValue(f.fieldKey, e.target.value)} required={f.isRequired} />
          ) : (
            <input value={values[f.fieldKey] ?? ""} onChange={(e) => setValue(f.fieldKey, e.target.value)} required={f.isRequired} />
          )}
        </div>
      ))}
    </div>
  );
}
