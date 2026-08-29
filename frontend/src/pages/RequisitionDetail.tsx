import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { getRequisition, submitRequisition, cancelRequisition } from "../api/procurement";
import { ApiError } from "../api/client";
import type { RequisitionDetail as RequisitionDetailDto } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

export function RequisitionDetail() {
  const { id } = useParams<{ id: string }>();
  const { api, ready } = useSession();
  const navigate = useNavigate();
  const [pr, setPr] = useState<RequisitionDetailDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    if (!id) return;
    getRequisition(api, id).then(setPr).catch((e) => setError(e instanceof ApiError ? e.message : "Failed to load."));
  };

  useEffect(() => {
    if (ready) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, ready, id]);

  const act = async (fn: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await fn();
      reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Action failed.");
    } finally {
      setBusy(false);
    }
  };

  if (!pr) return <div className="loading">{error ?? "Loading…"}</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>{pr.requisitionNumber} <StatusBadge status={pr.status} /></h1>
          <p>{pr.description}</p>
        </div>
        <div className="actions">
          {pr.status === "Draft" && (
            <button className="primary" disabled={busy} onClick={() => act(() => submitRequisition(api, pr.id))}>Submit for Approval</button>
          )}
          {(pr.status === "Draft" || pr.status === "PendingApproval") && (
            <button className="danger" disabled={busy} onClick={() => act(() => cancelRequisition(api, pr.id))}>Cancel</button>
          )}
          {pr.status === "Approved" && (
            <button className="primary" onClick={() => navigate(`/purchase-orders/new?fromRequisition=${pr.id}`)}>Create Purchase Order</button>
          )}
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="detail-grid">
        <div className="table-wrap">
          <table>
            <thead><tr><th>Item</th><th className="num">Qty</th><th>UOM</th><th className="num">Unit price</th><th className="num">Value</th></tr></thead>
            <tbody>
              {pr.lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.itemDescription}</td>
                  <td className="num">{l.quantity}</td>
                  <td>{l.uom}</td>
                  <td className="num">{l.estimatedUnitPrice.toLocaleString()}</td>
                  <td className="num">{l.estimatedValue.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card">
          <h3 style={{ marginBottom: "0.9rem" }}>Summary</h3>
          <div className="summary-list">
            <div className="row"><span className="k">Category</span><span>{pr.category}</span></div>
            <div className="row"><span className="k">Type</span><span>{pr.requisitionType}</span></div>
            <div className="row"><span className="k">Request date</span><span>{pr.requestDate}</span></div>
            <div className="row"><span className="k">Required by</span><span>{pr.requiredByDate}</span></div>
            <div className="row"><span className="k">Preferred supplier</span><span>{pr.preferredSupplierName ?? "—"}</span></div>
            <div className="row"><span className="k">Version</span><span>v{pr.currentVersionNumber}</span></div>
            <div className="row"><span className="k">Estimated value</span><b>{pr.currency} {pr.estimatedValue.toLocaleString()}</b></div>
          </div>
        </div>
      </div>

      <p className="hint" style={{ marginTop: "1rem" }}><Link to="/requisitions">&larr; Back to requisitions</Link></p>
    </>
  );
}
