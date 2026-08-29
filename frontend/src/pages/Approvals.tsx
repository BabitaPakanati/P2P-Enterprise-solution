import { useEffect, useState } from "react";
import { Check, X } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { myApprovals, decideApproval } from "../api/procurement";
import { ApiError } from "../api/client";
import type { ApprovalTask } from "../api/types";

export function Approvals() {
  const { api, ready } = useSession();
  const [rows, setRows] = useState<ApprovalTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [comments, setComments] = useState<Record<string, string>>({});
  const [busyTask, setBusyTask] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    myApprovals(api).then(setRows).finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready]);

  const decide = async (taskId: string, approve: boolean) => {
    setBusyTask(taskId);
    setError(null);
    try {
      await decideApproval(api, taskId, approve, comments[taskId]);
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Decision failed.");
    } finally {
      setBusyTask(null);
    }
  };

  return (
    <>
      <div className="page-header">
        <div>
          <h1>My Approvals</h1>
          <p>Tasks routed to you by the workflow engine, based on your role's authority assignment.</p>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead>
              <tr><th>Transaction</th><th>Type</th><th>Requester</th><th className="num">Amount</th><th>Comments</th><th style={{ width: 190 }}>Decision</th></tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={6} className="table-empty">Loading…</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={6} className="table-empty">Nothing waiting on you.</td></tr>
              ) : (
                rows.map((t) => (
                  <tr key={t.taskId}>
                    <td className="mono">{t.transactionNumber}</td>
                    <td>{t.entityType === "PurchaseRequisition" ? "Requisition" : "Purchase Order"}</td>
                    <td>{t.requester}</td>
                    <td className="num">{t.currency} {t.amount.toLocaleString()}</td>
                    <td>
                      <input
                        style={{ width: 180 }}
                        placeholder="Optional comment"
                        value={comments[t.taskId] ?? ""}
                        onChange={(e) => setComments((c) => ({ ...c, [t.taskId]: e.target.value }))}
                      />
                    </td>
                    <td className="actions-cell">
                      <button className="small primary" disabled={busyTask === t.taskId} onClick={() => decide(t.taskId, true)}><Check size={13} strokeWidth={2.5} />Approve</button>
                      <button className="small danger" disabled={busyTask === t.taskId} onClick={() => decide(t.taskId, false)}><X size={13} strokeWidth={2.5} />Reject</button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
