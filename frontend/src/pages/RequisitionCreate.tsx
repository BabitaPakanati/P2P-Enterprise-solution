import { useNavigate } from "react-router-dom";
import { Save } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { createRequisition } from "../api/procurement";
import { RequisitionForm } from "../components/RequisitionForm";

export function RequisitionCreate() {
  const { api } = useSession();
  const navigate = useNavigate();

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Create Purchase Requisition</h1>
          <p>Saved as a draft first - submit separately once it looks right.</p>
        </div>
      </div>

      <RequisitionForm
        submitLabel="Save Draft"
        submittingLabel="Saving…"
        submitIcon={<Save size={14} strokeWidth={2.25} />}
        onCancel={() => navigate("/requisitions")}
        onSubmit={async (values) => {
          const { id } = await createRequisition(api, {
            requiredByDate: values.requiredByDate,
            requisitionType: values.requisitionType,
            description: values.description,
            category: values.category,
            currency: "USD",
            preferredSupplierName: values.preferredSupplierName || undefined,
            lines: values.lines,
          });
          navigate(`/requisitions/${id}`);
        }}
      />
    </>
  );
}
