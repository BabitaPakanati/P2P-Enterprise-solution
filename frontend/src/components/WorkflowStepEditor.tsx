import { Plus, Trash2 } from "lucide-react";
import { RULE_OPERATORS, type RoleDto, type WorkflowRuleInput, type WorkflowStepInput } from "../api/settings";

const emptyRule = (): WorkflowRuleInput => ({ attribute: "Amount", operator: "LessOrEqual", value: "", conjunction: "And" });
const emptyStep = (sequence: number): WorkflowStepInput => ({ stepName: "", sequence, approvalRoleId: "", isMandatory: true, rules: [emptyRule()] });

interface WorkflowStepEditorProps {
  steps: WorkflowStepInput[];
  onChange: (steps: WorkflowStepInput[]) => void;
  roles: RoleDto[];
}

/**
 * Shared by "create a workflow" and "add a new version" - a step needs a name, who
 * approves it (a role, never a specific user - §22), and the conditions that decide
 * whether this step applies (rules the engine evaluates against the transaction's
 * attributes, e.g. Amount <= 100000). Most orgs will want one step with one rule;
 * this still allows more, matching what WorkflowEngine actually supports.
 */
export function WorkflowStepEditor({ steps, onChange, roles }: WorkflowStepEditorProps) {
  const updateStep = (i: number, patch: Partial<WorkflowStepInput>) =>
    onChange(steps.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));

  const updateRule = (stepIdx: number, ruleIdx: number, patch: Partial<WorkflowRuleInput>) =>
    onChange(steps.map((s, idx) => (idx !== stepIdx ? s : { ...s, rules: s.rules.map((r, ri) => (ri === ruleIdx ? { ...r, ...patch } : r)) })));

  return (
    <div>
      {steps.map((step, i) => (
        <div className="card" key={i} style={{ marginBottom: "0.9rem", maxWidth: "none" }}>
          <div className="field-row" style={{ marginBottom: "0.7rem" }}>
            <div className="field">
              <label>Step name</label>
              <input value={step.stepName} onChange={(e) => updateStep(i, { stepName: e.target.value })} placeholder="e.g. Manager Approval" />
            </div>
            <div className="field">
              <label>Approval role</label>
              <select value={step.approvalRoleId} onChange={(e) => updateStep(i, { approvalRoleId: e.target.value })}>
                <option value="">Select a role…</option>
                {roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label>&nbsp;</label>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", fontWeight: 400 }}>
                <input type="checkbox" style={{ width: "auto" }} checked={step.isMandatory} onChange={(e) => updateStep(i, { isMandatory: e.target.checked })} />
                Mandatory step
              </label>
            </div>
          </div>

          <table className="line-table">
            <thead><tr><th>Attribute</th><th style={{ width: 150 }}>Operator</th><th style={{ width: 130 }}>Value</th><th></th></tr></thead>
            <tbody>
              {step.rules.map((rule, ri) => (
                <tr key={ri}>
                  <td><input value={rule.attribute} onChange={(e) => updateRule(i, ri, { attribute: e.target.value })} placeholder="Amount" /></td>
                  <td>
                    <select value={rule.operator} onChange={(e) => updateRule(i, ri, { operator: e.target.value })}>
                      {RULE_OPERATORS.map((op) => <option key={op} value={op}>{op}</option>)}
                    </select>
                  </td>
                  <td><input value={rule.value} onChange={(e) => updateRule(i, ri, { value: e.target.value })} placeholder="100000" /></td>
                  <td>
                    {step.rules.length > 1 && (
                      <button type="button" className="small danger" onClick={() => updateStep(i, { rules: step.rules.filter((_, ridx) => ridx !== ri) })}>
                        <Trash2 size={12} strokeWidth={2.25} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.6rem" }}>
            <button type="button" className="small" onClick={() => updateStep(i, { rules: [...step.rules, emptyRule()] })}><Plus size={12} strokeWidth={2.25} />Add condition</button>
            {steps.length > 1 && (
              <button type="button" className="small danger" onClick={() => onChange(steps.filter((_, idx) => idx !== i))}><Trash2 size={12} strokeWidth={2.25} />Remove step</button>
            )}
          </div>
        </div>
      ))}
      <button type="button" onClick={() => onChange([...steps, emptyStep(steps.length + 1)])}><Plus size={13} strokeWidth={2.25} />Add step</button>
    </div>
  );
}

export { emptyStep };
