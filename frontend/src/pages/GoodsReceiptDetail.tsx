import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Send, XCircle, PenSquare } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getGoodsReceipt, getGoodsReceiptVersions, postGoodsReceipt, cancelGoodsReceipt, amendGoodsReceipt } from "../api/receiving";
import { ApiError } from "../api/client";
import type { GoodsReceiptDetail as GoodsReceiptDetailDto, DocumentVersion, CreateGoodsReceiptLineInput } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";
import { DynamicFields, type CustomFieldValues } from "../components/DynamicFields";

interface GrSnapshotLine { ItemDescription: string; Uom: string; QuantityOrdered: number; QuantityReceived: number; QuantityAccepted: number; QuantityRejected: number; InspectionStatus: string }
interface GrSnapshot { DeliveryDate: string; DeliveryNoteNumber: string | null; Location: string | null; Lines: GrSnapshotLine[] }

export function GoodsReceiptDetail() {
  const { id } = useParams<{ id: string }>();
  const { api, ready } = useSession();
  const [gr, setGr] = useState<GoodsReceiptDetailDto | null>(null);
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [tab, setTab] = useState<"overview" | "history" | "amend">("overview");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = () => {
    if (!id) return;
    getGoodsReceipt(api, id).then(setGr).catch((e) => setError(e instanceof ApiError ? e.message : "Failed to load."));
    getGoodsReceiptVersions(api, id).then(setVersions);
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

  if (!gr) return <div className="loading">{error ?? "Loading…"}</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1 className="mono">{gr.receiptNumber} <StatusBadge status={gr.status} /></h1>
          <p>Against <Link to={`/purchase-orders/${gr.purchaseOrderId}`}>{gr.poNumber}</Link> · {gr.supplierName} · version {gr.currentVersionNumber}</p>
        </div>
        <div className="actions">
          {gr.status === "Draft" && <button className="primary" disabled={busy} onClick={() => act(() => postGoodsReceipt(api, gr.id))}><Send size={14} strokeWidth={2.25} />Post</button>}
          {gr.status === "Draft" && <button className="danger" disabled={busy} onClick={() => act(() => cancelGoodsReceipt(api, gr.id))}><XCircle size={14} strokeWidth={2.25} />Cancel Receipt</button>}
          {gr.status === "Posted" && <button disabled={busy} onClick={() => setTab("amend")}><PenSquare size={14} strokeWidth={2.25} />Correct</button>}
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="tabs">
        <button className={tab === "overview" ? "active" : ""} onClick={() => setTab("overview")}>Overview</button>
        <button className={tab === "history" ? "active" : ""} onClick={() => setTab("history")}>Version History ({versions.length})</button>
        {gr.status === "Posted" && <button className={tab === "amend" ? "active" : ""} onClick={() => setTab("amend")}>Correct</button>}
      </div>

      {tab === "overview" && (
        <div className="detail-grid">
          <div className="table-wrap">
            <div className="table-scroll">
              <table>
                <thead><tr><th>Item</th><th className="num">Ordered</th><th className="num">Received</th><th className="num">Accepted</th><th className="num">Rejected</th><th>Inspection</th></tr></thead>
                <tbody>
                  {gr.lines.map((l) => (
                    <tr key={l.id}>
                      <td>{l.itemDescription} <span className="hint">({l.uom})</span></td>
                      <td className="num">{l.quantityOrdered}</td><td className="num">{l.quantityReceived}</td>
                      <td className="num">{l.quantityAccepted}</td><td className="num">{l.quantityRejected}</td>
                      <td><StatusBadge status={l.inspectionStatus} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          <div className="card">
            <h3>Summary</h3>
            <div className="summary-list">
              <div className="row"><span className="k">Delivery date</span><span className="num">{gr.deliveryDate}</span></div>
              <div className="row"><span className="k">Delivery note #</span><span>{gr.deliveryNoteNumber ?? "—"}</span></div>
              <div className="row"><span className="k">Location</span><span>{gr.location ?? "—"}</span></div>
              <div className="row"><span className="k">Purchase order</span><Link to={`/purchase-orders/${gr.purchaseOrderId}`}>View</Link></div>
            </div>
          </div>
        </div>
      )}

      {tab === "history" && (
        <div className="version-list">
          {versions.map((v) => {
            const snap = JSON.parse(v.payloadJson) as GrSnapshot;
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
                    <div><div className="k">Delivery note #</div><div className="v">{snap.DeliveryNoteNumber ?? "—"}</div></div>
                    <div><div className="k">Location</div><div className="v">{snap.Location ?? "—"}</div></div>
                    {v.changeReason && <div style={{ gridColumn: "1 / -1" }}><div className="k">Change reason</div><div className="v">{v.changeReason}</div></div>}
                  </div>
                  <div className="version-lines">
                    <table>
                      <thead><tr><th>Item</th><th className="num">Received</th><th className="num">Accepted</th><th className="num">Rejected</th><th>Inspection</th></tr></thead>
                      <tbody>
                        {snap.Lines.map((l, i) => (
                          <tr key={i}><td>{l.ItemDescription}</td><td className="num">{l.QuantityReceived}</td><td className="num">{l.QuantityAccepted}</td><td className="num">{l.QuantityRejected}</td><td>{l.InspectionStatus}</td></tr>
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

      {tab === "amend" && <CorrectForm gr={gr} onDone={() => { setTab("overview"); reload(); }} onCancel={() => setTab("overview")} />}
    </>
  );
}

function CorrectForm({ gr, onDone, onCancel }: { gr: GoodsReceiptDetailDto; onDone: () => void; onCancel: () => void }) {
  const { api } = useSession();
  const [deliveryDate, setDeliveryDate] = useState(gr.deliveryDate);
  const [deliveryNoteNumber, setDeliveryNoteNumber] = useState(gr.deliveryNoteNumber ?? "");
  const [location, setLocation] = useState(gr.location ?? "");
  const [changeReason, setChangeReason] = useState("");
  const [lines, setLines] = useState(gr.lines.map((l) => ({
    purchaseOrderLineId: l.purchaseOrderLineId, itemDescription: l.itemDescription, uom: l.uom,
    quantityReceived: String(l.quantityReceived), quantityRejected: String(l.quantityRejected),
  })));
  const [customFields, setCustomFields] = useState<CustomFieldValues>(gr.customFields);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateLine = (i: number, patch: Partial<{ quantityReceived: string; quantityRejected: string }>) =>
    setLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!changeReason.trim()) { setError("A change reason is required for a correction."); return; }
    setSaving(true);
    setError(null);
    try {
      const submittedLines: CreateGoodsReceiptLineInput[] = lines
        .filter((l) => Number(l.quantityReceived) > 0)
        .map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, quantityReceived: Number(l.quantityReceived), quantityRejected: Number(l.quantityRejected) || 0 }));
      await amendGoodsReceipt(api, gr.id, {
        deliveryDate, deliveryNoteNumber: deliveryNoteNumber || undefined, location: location || undefined, changeReason,
        lines: submittedLines, customFields,
      });
      onDone();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Correction failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="card" onSubmit={submit}>
      <p className="hint" style={{ marginBottom: "1rem" }}>
        This corrects the posted receipt in place - version {gr.currentVersionNumber} is superseded and stays in the history below.
      </p>
      {error && <div className="error-banner">{error}</div>}

      <div className="field-row">
        <div className="field"><label>Delivery date</label><input type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} required /></div>
        <div className="field"><label>Delivery note #</label><input value={deliveryNoteNumber} onChange={(e) => setDeliveryNoteNumber(e.target.value)} /></div>
        <div className="field"><label>Location</label><input value={location} onChange={(e) => setLocation(e.target.value)} /></div>
      </div>
      <div className="field" style={{ marginBottom: "1rem" }}>
        <label>Change reason</label>
        <input value={changeReason} onChange={(e) => setChangeReason(e.target.value)} placeholder="e.g. Inspector re-evaluated the rejected unit" required />
      </div>

      <table className="line-table">
        <thead><tr><th>Item</th><th style={{ width: 110 }}>Qty received</th><th style={{ width: 110 }}>Qty rejected</th></tr></thead>
        <tbody>
          {lines.map((l, i) => (
            <tr key={l.purchaseOrderLineId}>
              <td>{l.itemDescription} <span className="hint">({l.uom})</span></td>
              <td><input type="number" min={0} step="0.01" value={l.quantityReceived} onChange={(e) => updateLine(i, { quantityReceived: e.target.value })} /></td>
              <td><input type="number" min={0} step="0.01" value={l.quantityRejected} onChange={(e) => updateLine(i, { quantityRejected: e.target.value })} /></td>
            </tr>
          ))}
        </tbody>
      </table>

      <DynamicFields entityType="GoodsReceipt" values={customFields} onChange={setCustomFields} />

      <div className="form-actions">
        <button type="submit" className="primary" disabled={saving}>{saving ? "Saving…" : "Save Correction"}</button>
        <button type="button" disabled={saving} onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}
