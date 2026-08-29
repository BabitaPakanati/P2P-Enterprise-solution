import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Send, XCircle, ShoppingCart, PenSquare } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getRequisition, getRequisitionVersions, submitRequisition, cancelRequisition, amendRequisition } from "../api/procurement";
import { ApiError } from "../api/client";
import type { RequisitionDetail as RequisitionDetailDto, DocumentVersion } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";
import { RequisitionForm } from "../components/RequisitionForm";

interface PrSnapshotLine { ItemDescription: string; Quantity: number; Uom: string; EstimatedUnitPrice: number }
interface PrSnapshot { Description: string; Category: string; RequisitionType: string; RequiredByDate: string; PreferredSupplierName: string | null; EstimatedValue: number; Currency: string; Lines: PrSnapshotLine[] }

export function RequisitionDetail() {
  const { id } = useParams<{ id: string }>();
  const { api, ready } = useSession();
  const navigate = useNavigate();
  const [pr, setPr] = useState<RequisitionDetailDto | null>(null);
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [tab, setTab] = useState<"overview" | "history" | "amend">("overview");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    if (!id) return;
    getRequisition(api, id).then(setPr).catch((e) => setError(e instanceof ApiError ? e.message : "Failed to load."));
    getRequisitionVersions(api, id).then(setVersions);
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
          <h1 className="mono">{pr.requisitionNumber} <StatusBadge status={pr.status} /></h1>
          <p>{pr.description}</p>
        </div>
        <div className="actions">
          {pr.status === "Draft" && (
            <button onClick={() => navigate(`/requisitions/${pr.id}/edit`)}><PenSquare size={14} strokeWidth={2.25} />Edit</button>
          )}
          {pr.status === "Draft" && (
            <button className="primary" disabled={busy} onClick={() => act(() => submitRequisition(api, pr.id))}><Send size={14} strokeWidth={2.25} />Submit for Approval</button>
          )}
          {(pr.status === "Draft" || pr.status === "PendingApproval") && (
            <button className="danger" disabled={busy} onClick={() => act(() => cancelRequisition(api, pr.id))}><XCircle size={14} strokeWidth={2.25} />Cancel</button>
          )}
          {pr.status === "Approved" && (
            <button onClick={() => setTab("amend")}><PenSquare size={14} strokeWidth={2.25} />Amend</button>
          )}
          {pr.status === "Approved" && (
            <button className="primary" onClick={() => navigate(`/purchase-orders/new?fromRequisition=${pr.id}`)}><ShoppingCart size={14} strokeWidth={2.25} />Create Purchase Order</button>
          )}
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="tabs">
        <button className={tab === "overview" ? "active" : ""} onClick={() => setTab("overview")}>Overview</button>
        <button className={tab === "history" ? "active" : ""} onClick={() => setTab("history")}>Version History ({versions.length})</button>
        {pr.status === "Approved" && (
          <button className={tab === "amend" ? "active" : ""} onClick={() => setTab("amend")}>Amend</button>
        )}
      </div>

      {tab === "overview" && (
        <div className="detail-grid">
          <div className="table-wrap">
            <div className="table-scroll">
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
          </div>

          <div className="card">
            <h3>Summary</h3>
            <div className="summary-list">
              <div className="row"><span className="k">Category</span><span>{pr.category}</span></div>
              <div className="row"><span className="k">Type</span><span>{pr.requisitionType}</span></div>
              <div className="row"><span className="k">Request date</span><span className="num">{pr.requestDate}</span></div>
              <div className="row"><span className="k">Required by</span><span className="num">{pr.requiredByDate}</span></div>
              <div className="row"><span className="k">Preferred supplier</span><span>{pr.preferredSupplierName ?? "—"}</span></div>
              <div className="row"><span className="k">Version</span><span className="mono">v{pr.currentVersionNumber}</span></div>
              <div className="row"><span className="k">Estimated value</span><span className="v-strong">{pr.currency} {pr.estimatedValue.toLocaleString()}</span></div>
            </div>
          </div>
        </div>
      )}

      {tab === "history" && (
        <div className="version-list">
          {versions.map((v) => {
            const snap = JSON.parse(v.payloadJson) as PrSnapshot;
            return (
              <div className={`version-card${v.versionStatus === "Active" ? " is-active" : ""}`} key={v.id}>
                <div className="vbody">
                  <div className="vhead">
                    <span className="vnum">Version {v.versionNumber}</span>
                    <StatusBadge status={v.versionStatus} />
                  </div>
                  <div className="version-kv">
                    <div><div className="k">Effective from</div><div className="v">{new Date(v.effectiveFrom).toLocaleString()}</div></div>
                    <div><div className="k">Effective to</div><div className="v">{v.effectiveTo ? new Date(v.effectiveTo).toLocaleString() : "current"}</div></div>
                    <div><div className="k">Description</div><div className="v">{snap.Description}</div></div>
                    <div><div className="k">Estimated value</div><div className="v num">{snap.Currency} {snap.EstimatedValue.toLocaleString()}</div></div>
                    {v.changeReason && <div style={{ gridColumn: "1 / -1" }}><div className="k">Change reason</div><div className="v">{v.changeReason}</div></div>}
                  </div>
                  <div className="version-lines">
                    <table>
                      <thead><tr><th>Item</th><th className="num">Qty</th><th>UOM</th><th className="num">Unit price</th></tr></thead>
                      <tbody>
                        {snap.Lines.map((l, i) => (
                          <tr key={i}><td>{l.ItemDescription}</td><td className="num">{l.Quantity}</td><td>{l.Uom}</td><td className="num">{l.EstimatedUnitPrice.toLocaleString()}</td></tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {tab === "amend" && (
        <RequisitionForm
          initial={{
            description: pr.description, category: pr.category, requisitionType: pr.requisitionType,
            requiredByDate: pr.requiredByDate, preferredSupplierName: pr.preferredSupplierName ?? "",
            lines: pr.lines.map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity, uom: l.uom, estimatedUnitPrice: l.estimatedUnitPrice })),
          }}
          requireChangeReason
          hint={`This creates a new pending version — version ${pr.currentVersionNumber} stays the effective one until this is approved.`}
          submitLabel="Submit Amendment for Approval"
          submittingLabel="Submitting…"
          submitIcon={<Send size={14} strokeWidth={2.25} />}
          onSubmit={async (values, changeReason) => {
            await amendRequisition(api, pr.id, {
              requiredByDate: values.requiredByDate,
              requisitionType: values.requisitionType,
              description: values.description,
              category: values.category,
              currency: "USD",
              preferredSupplierName: values.preferredSupplierName || undefined,
              changeReason,
              lines: values.lines,
            });
            setTab("overview");
            reload();
          }}
        />
      )}

      <p className="hint" style={{ marginTop: "1.2rem" }}>
        <Link to="/requisitions" style={{ display: "inline-flex", alignItems: "center", gap: "0.3rem" }}>
          <ArrowLeft size={13} strokeWidth={2.25} /> Back to requisitions
        </Link>
      </p>
    </>
  );
}
