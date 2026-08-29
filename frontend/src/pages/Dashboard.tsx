import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FileText, Clock, ShoppingCart, Wallet, CheckSquare, ArrowRight } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { listRequisitions, listOrders, myApprovals } from "../api/procurement";
import type { RequisitionSummary, OrderSummary, ApprovalTask } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

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

  const kpis = [
    { label: "Requisitions", value: requisitions.length, icon: FileText },
    { label: "Pending Approval", value: pendingRequisitions, icon: Clock },
    { label: "Purchase Orders", value: orders.length, icon: ShoppingCart },
    { label: "Open PO Value", value: `$${openOrderValue.toLocaleString()}`, icon: Wallet },
    { label: "My Approvals", value: approvals.length, icon: CheckSquare },
  ];

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
            {kpis.map(({ label, value, icon: Icon }) => (
              <div className="kpi-card" key={label}>
                <div className="kpi-top">
                  <span className="label">{label}</span>
                  <span className="icon-chip"><Icon size={15} strokeWidth={2} /></span>
                </div>
                <div className="value">{value}</div>
              </div>
            ))}
          </div>

          <div className="detail-grid">
            <div className="table-wrap">
              <div className="table-scroll">
                <table>
                  <thead><tr><th>Requisition</th><th>Description</th><th>Status</th><th className="num">Value</th></tr></thead>
                  <tbody>
                    {requisitions.slice(0, 6).map((r) => (
                      <tr key={r.id}>
                        <td><Link to={`/requisitions/${r.id}`} className="mono">{r.requisitionNumber}</Link></td>
                        <td>{r.description}</td>
                        <td><StatusBadge status={r.status} /></td>
                        <td className="num">{r.currency} {r.estimatedValue.toLocaleString()}</td>
                      </tr>
                    ))}
                    {requisitions.length === 0 && (
                      <tr><td colSpan={4} className="table-empty">No requisitions yet. <Link to="/requisitions/new">Create one</Link>.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="table-wrap">
              <div className="table-scroll">
                <table>
                  <thead><tr><th>My approvals</th><th></th></tr></thead>
                  <tbody>
                    {approvals.slice(0, 6).map((a) => (
                      <tr key={a.taskId}>
                        <td>{a.transactionNumber} · {a.currency} {a.amount.toLocaleString()}</td>
                        <td><Link to="/approvals"><ArrowRight size={14} /></Link></td>
                      </tr>
                    ))}
                    {approvals.length === 0 && <tr><td colSpan={2} className="table-empty">Nothing pending.</td></tr>}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}
