import { Routes, Route, Navigate } from "react-router-dom";
import { Layout } from "./components/Layout";
import { AdminLayout } from "./components/AdminLayout";
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
import { Roles } from "./pages/settings/Roles";
import { Workflows } from "./pages/settings/Workflows";
import { AdminLogin } from "./pages/admin/AdminLogin";
import { OrganisationsList } from "./pages/admin/OrganisationsList";
import { useSession } from "./context/SessionContext";
import { useAdminSession } from "./context/AdminSessionContext";

function RequireAuth({ children }: { children: React.ReactElement }) {
  const { ready } = useSession();
  return ready ? children : <Navigate to="/login" replace />;
}

function RequireAdminAuth({ children }: { children: React.ReactElement }) {
  const { ready } = useAdminSession();
  return ready ? children : <Navigate to="/admin/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/admin/login" element={<AdminLogin />} />

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
        <Route path="/settings/roles" element={<Roles />} />
        <Route path="/settings/workflows" element={<Workflows />} />
      </Route>

      <Route
        path="/admin"
        element={
          <RequireAdminAuth>
            <AdminLayout />
          </RequireAdminAuth>
        }
      >
        <Route index element={<Navigate to="organisations" replace />} />
        <Route path="organisations" element={<OrganisationsList />} />
      </Route>
    </Routes>
  );
}
