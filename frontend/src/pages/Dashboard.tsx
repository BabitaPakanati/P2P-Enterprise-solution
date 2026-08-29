import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { listRequisitions, listOrders, myApprovals } from "../api/procurement";
import type { RequisitionSummary, OrderSummary, ApprovalTask } from "../api/types";

export function Dashboard() {
  const { api, ready } = useSession();
  const [requisitions, setRequisitions] = useState<RequisitionSummary[]>([]);
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [approvals, setApprovals] = useState<ApprovalTask[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!ready) return;
    setLoading(true);
    Promise.all([listRequisitions(api, false), listOrders(api), myApprovals(api)])
      .then(([r, o, a]) => {
        setRequisitions(r);
        setOrders(o);
        setApprovals(a);
      })
      .finally(() => setLoading(false));
  }, [api, ready]);

  const pendingRequisitions = requisitions.filter((r) => r.status === "PendingApproval").length;
  const openOrderValue = orders
    .filter((o) => o.status !== "Cancelled" && o.status !== "Closed")
    .reduce((sum, o) => sum + o.totalValue, 0);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p>Requisition → PO snapshot for this organisation.</p>
        </div>
      </div>

      {loading ? (
        <div className="loading">Loading…</div>
      ) : (
        <>
          <div className="kpi-row">
            <div className="kpi-card"><div className="label">Requisitions</div><div className="value">{requisitions.length}</div></div>
            <div className="kpi-card"><div className="label">Pending Approval</div><div className="value">{pendingRequisitions}</div></div>
            <div className="kpi-card"><div className="label">Purchase Orders</div><div className="value">{orders.length}</div></div>
            <div className="kpi-card"><div className="label">Open PO Value</div><div className="value">${openOrderValue.toLocaleString()}</div></div>
            <div className="kpi-card"><div className="label">My Approvals</div><div className="value">{approvals.length}</div></div>
          </div>

          <div className="detail-grid">
            <div className="table-wrap">
              <table>
                <thead><tr><th>Requisition</th><th>Description</th><th>Status</th><th className="num">Value</th></tr></thead>
                <tbody>
                  {requisitions.slice(0, 6).map((r) => (
                    <tr key={r.id}>
                      <td><Link to={`/requisitions/${r.id}`}>{r.requisitionNumber}</Link></td>
                      <td>{r.description}</td>
                      <td>{r.status}</td>
                      <td className="num">{r.currency} {r.estimatedValue.toLocaleString()}</td>
                    </tr>
                  ))}
                  {requisitions.length === 0 && (
                    <tr><td colSpan={4} className="table-empty">No requisitions yet. <Link to="/requisitions/new">Create one</Link>.</td></tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="table-wrap">
              <table>
                <thead><tr><th>My approvals</th></tr></thead>
                <tbody>
                  {approvals.slice(0, 6).map((a) => (
                    <tr key={a.taskId}>
                      <td>{a.transactionNumber} · {a.currency} {a.amount.toLocaleString()}</td>
                    </tr>
                  ))}
                  {approvals.length === 0 && <tr><td className="table-empty">Nothing pending.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </>
  );
}
