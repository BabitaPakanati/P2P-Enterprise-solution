import { Routes, Route } from "react-router-dom";
import { Layout } from "./components/Layout";
import { Dashboard } from "./pages/Dashboard";
import { RequisitionsList } from "./pages/RequisitionsList";
import { RequisitionCreate } from "./pages/RequisitionCreate";
import { RequisitionDetail } from "./pages/RequisitionDetail";
import { Approvals } from "./pages/Approvals";
import { PurchaseOrdersList } from "./pages/PurchaseOrdersList";
import { PurchaseOrderCreate } from "./pages/PurchaseOrderCreate";
import { PurchaseOrderDetail } from "./pages/PurchaseOrderDetail";

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<Dashboard />} />
        <Route path="/requisitions" element={<RequisitionsList />} />
        <Route path="/requisitions/new" element={<RequisitionCreate />} />
        <Route path="/requisitions/:id" element={<RequisitionDetail />} />
        <Route path="/approvals" element={<Approvals />} />
        <Route path="/purchase-orders" element={<PurchaseOrdersList />} />
        <Route path="/purchase-orders/new" element={<PurchaseOrderCreate />} />
        <Route path="/purchase-orders/:id" element={<PurchaseOrderDetail />} />
      </Route>
    </Routes>
  );
}
