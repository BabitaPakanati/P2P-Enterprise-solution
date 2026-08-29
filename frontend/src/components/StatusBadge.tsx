const GOOD = new Set(["Approved", "Active", "SentToSupplier", "Confirmed", "Posted", "Accepted", "FullyReceived"]);
const WARN = new Set(["PendingApproval", "Draft", "Ordered", "PartiallyReceived", "PartiallyAccepted"]);
const CRIT = new Set(["Rejected", "Cancelled", "Superseded"]);

export function StatusBadge({ status }: { status: string }) {
  const cls = GOOD.has(status) ? "good" : WARN.has(status) ? "warn" : CRIT.has(status) ? "crit" : "open";
  const label = status.replace(/([a-z])([A-Z])/g, "$1 $2");
  return (
    <span className={`badge ${cls}`}>
      <span className="dot" />
      {label}
    </span>
  );
}
