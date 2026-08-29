import { Routes, Route, Navigate } from "react-router-dom";
import { Layout } from "./components/Layout";
import { Login } from "./pages/Login";
import { Dashboard } from "./pages/Dashboard";
import { RequisitionsList } from "./pages/RequisitionsList";
import { RequisitionCreate } from "./pages/RequisitionCreate";
import { RequisitionEdit } from "./pages/RequisitionEdit";
import { RequisitionDetail } from "./pages/RequisitionDetail";
import { Approvals } from "./pages/Approvals";
import { PurchaseOrdersList } from "./pages/PurchaseOrdersList";
import { PurchaseOrderCreate } from "./pages/PurchaseOrderCreate";
import { PurchaseOrderDetail } from "./pages/PurchaseOrderDetail";
import { useSession } from "./context/SessionContext";

function RequireAuth({ children }: { children: React.ReactElement }) {
  const { ready } = useSession();
  return ready ? children : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route path="/" element={<Dashboard />} />
        <Route path="/requisitions" element={<RequisitionsList />} />
        <Route path="/requisitions/new" element={<RequisitionCreate />} />
        <Route path="/requisitions/:id/edit" element={<RequisitionEdit />} />
        <Route path="/requisitions/:id" element={<RequisitionDetail />} />
        <Route path="/approvals" element={<Approvals />} />
        <Route path="/purchase-orders" element={<PurchaseOrdersList />} />
        <Route path="/purchase-orders/new" element={<PurchaseOrderCreate />} />
        <Route path="/purchase-orders/:id" element={<PurchaseOrderDetail />} />
      </Route>
    </Routes>
  );
}
