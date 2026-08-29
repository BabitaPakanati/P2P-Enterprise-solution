import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../context/SessionContext";
import { listGoodsReceipts } from "../api/receiving";
import type { GoodsReceiptSummary } from "../api/types";
import { StatusBadge } from "../components/StatusBadge";

export function GoodsReceiptsList() {
  const { api, ready } = useSession();
  const [rows, setRows] = useState<GoodsReceiptSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!ready) return;
    setLoading(true);
    listGoodsReceipts(api).then(setRows).finally(() => setLoading(false));
  }, [api, ready]);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Goods Receipts</h1>
          <p>What's actually arrived against a purchase order. Record one from the PO you're receiving against.</p>
        </div>
      </div>

      <div className="table-wrap">
        <div className="table-scroll">
          <table>
            <thead>
              <tr><th>Receipt #</th><th>PO #</th><th>Supplier</th><th>Delivery date</th><th>Status</th></tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={5} className="table-empty">Loading…</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={5} className="table-empty">No goods receipts yet — record one from an Approved or SentToSupplier purchase order.</td></tr>
              ) : (
                rows.map((g) => (
                  <tr key={g.id}>
                    <td><Link to={`/goods-receipts/${g.id}`} className="mono">{g.receiptNumber}</Link></td>
                    <td><Link to={`/purchase-orders/${g.purchaseOrderId}`} className="mono">{g.poNumber}</Link></td>
                    <td>{g.supplierName}</td>
                    <td className="num">{g.deliveryDate}</td>
                    <td><StatusBadge status={g.status} /></td>
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
