import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { listRequisitions } from "../api/procurement";
import type { RequisitionSummary } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

export function RequisitionsList() {
  const { api, ready } = useSession();
  const [rows, setRows] = useState<RequisitionSummary[]>([]);
  const [mineOnly, setMineOnly] = useState(true);
  const [loading, setLoading] = useState(true);

  const reload = () => {
    setLoading(true);
    listRequisitions(api, mineOnly).then(setRows).finally(() => setLoading(false));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready, mineOnly]);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>{mineOnly ? "My Requisitions" : "All Requisitions"}</h1>
          <p>Demand → requisition. Create, submit for approval, track status.</p>
        </div>
        <div className="actions">
          <button onClick={() => setMineOnly((v) => !v)}>{mineOnly ? "Show all" : "Show mine only"}</button>
          <Link to="/requisitions/new"><button className="primary">+ Create Requisition</button></Link>
        </div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Requisition #</th><th>Description</th><th>Category</th><th>Required by</th>
              <th className="num">Est. Value</th><th>Status</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={6} className="table-empty">Loading…</td></tr>
            ) : rows.length === 0 ? (
              <tr><td colSpan={6} className="table-empty">No requisitions found.</td></tr>
            ) : (
              rows.map((r) => (
                <tr key={r.id}>
                  <td><Link to={`/requisitions/${r.id}`}>{r.requisitionNumber}</Link></td>
                  <td>{r.description}</td>
                  <td>{r.category}</td>
                  <td>{r.requiredByDate}</td>
                  <td className="num">{r.currency} {r.estimatedValue.toLocaleString()}</td>
                  <td><StatusBadge status={r.status} /></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}
