import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Save } from "lucide-react";
import { useSession } from "../context/SessionContext";
import { getRequisition, updateRequisition } from "../api/procurement";
import type { RequisitionDetail } from "../api/types";
import { RequisitionForm } from "../components/RequisitionForm";

export function RequisitionEdit() {
  const { id } = useParams<{ id: string }>();
  const { api } = useSession();
  const navigate = useNavigate();
  const [pr, setPr] = useState<RequisitionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    getRequisition(api, id).then((r) => {
      if (r.status !== "Draft") {
        setError(`This requisition is '${r.status}', not Draft - it can no longer be edited this way.`);
      }
      setPr(r);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (!pr) return <div className="loading">Loading…</div>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1 className="mono">Edit {pr.requisitionNumber}</h1>
          <p>Changes replace the draft outright - there's nothing to preserve yet, since it hasn't been submitted.</p>
        </div>
      </div>

      {error ? (
        <div className="error-banner" style={{ maxWidth: 800 }}>{error}</div>
      ) : (
        <RequisitionForm
          initial={{
            description: pr.description, category: pr.category, requisitionType: pr.requisitionType,
            requiredByDate: pr.requiredByDate, preferredSupplierName: pr.preferredSupplierName ?? "",
            lines: pr.lines.map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity, uom: l.uom, estimatedUnitPrice: l.estimatedUnitPrice })),
          }}
          submitLabel="Save Changes"
          submittingLabel="Saving…"
          submitIcon={<Save size={14} strokeWidth={2.25} />}
          onSubmit={async (values) => {
            await updateRequisition(api, pr.id, {
              requiredByDate: values.requiredByDate,
              requisitionType: values.requisitionType,
              description: values.description,
              category: values.category,
              currency: "USD",
              preferredSupplierName: values.preferredSupplierName || undefined,
              lines: values.lines,
            });
            navigate(`/requisitions/${pr.id}`);
          }}
        />
      )}
    </>
  );
}
