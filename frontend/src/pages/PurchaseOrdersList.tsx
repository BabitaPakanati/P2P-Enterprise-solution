import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { listOrders } from "../api/procurement";
import type { OrderSummary } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

export function PurchaseOrdersList() {
  const { api, ready } = useSession();
  const [rows, setRows] = useState<OrderSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!ready) return;
    setLoading(true);
    listOrders(api).then(setRows).finally(() => setLoading(false));
  }, [api, ready]);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Purchase Orders</h1>
          <p>Generated from approved requisitions. Amending one always creates a new version.</p>
        </div>
      </div>

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead>
              <tr><th>PO #</th><th>Supplier</th><th>PO date</th><th>Delivery</th><th className="num">Total</th><th>Status</th></tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={6} className="table-empty">Loading…</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={6} className="table-empty">No purchase orders yet — approve a requisition, then create one from it.</td></tr>
              ) : (
                rows.map((o) => (
                  <tr key={o.id}>
                    <td><Link to={`/purchase-orders/${o.id}`} className="mono">{o.poNumber}</Link></td>
                    <td>{o.supplierName}</td>
                    <td className="num">{o.poDate}</td>
                    <td className="num">{o.deliveryDate ?? "—"}</td>
                    <td className="num">{o.currency} {o.totalValue.toLocaleString()}</td>
                    <td><StatusBadge status={o.status} /></td>
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
