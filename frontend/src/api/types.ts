export interface RequisitionLine {
  id: string;
  lineNumber: number;
  itemDescription: string;
  quantity: number;
  uom: string;
  estimatedUnitPrice: number;
  estimatedValue: number;
}

export interface RequisitionSummary {
  id: string;
  requisitionNumber: string;
  requesterId: string;
  requestDate: string;
  requiredByDate: string;
  category: string;
  description: string;
  estimatedValue: number;
  currency: string;
  status: string;
}

export interface RequisitionDetail extends RequisitionSummary {
  documentId: string;
  requisitionType: string;
  preferredSupplierName: string | null;
  currentVersionNumber: number;
  lines: RequisitionLine[];
}

export interface CreateRequisitionLineInput {
  itemDescription: string;
  quantity: number;
  uom: string;
  estimatedUnitPrice: number;
}

export interface CreateRequisitionInput {
  requiredByDate: string;
  requisitionType: string;
  description: string;
  category: string;
  currency: string;
  preferredSupplierName?: string;
  lines: CreateRequisitionLineInput[];
}

/** Same shape as create - editing a Draft replaces the whole thing. */
export type UpdateRequisitionInput = CreateRequisitionInput;

export interface AmendRequisitionInput extends CreateRequisitionInput {
  changeReason: string;
}

export interface OrderLine {
  id: string;
  lineNumber: number;
  itemDescription: string;
  quantity: number;
  uom: string;
  unitPrice: number;
  lineValue: number;
}

export interface OrderSummary {
  id: string;
  poNumber: string;
  supplierName: string;
  poDate: string;
  deliveryDate: string | null;
  totalValue: number;
  currency: string;
  status: string;
}

export interface OrderDetail extends OrderSummary {
  documentId: string;
  sourceRequisitionId: string;
  buyerId: string;
  currentVersionNumber: number;
  lines: OrderLine[];
}

export interface CreateOrderLineInput {
  itemDescription: string;
  quantity: number;
  uom: string;
  unitPrice: number;
}

export interface CreatePurchaseOrderInput {
  sourceRequisitionId: string;
  supplierName: string;
  deliveryDate?: string;
  lines: CreateOrderLineInput[];
}

export interface AmendPurchaseOrderInput {
  supplierName: string;
  deliveryDate?: string;
  changeReason: string;
  lines: CreateOrderLineInput[];
}

export interface DocumentVersion {
  id: string;
  versionNumber: number;
  versionStatus: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  changeReason: string | null;
  changeComment: string | null;
  payloadJson: string;
}

export interface ApprovalTask {
  taskId: string;
  workflowInstanceId: string;
  entityType: string;
  entityId: string;
  transactionNumber: string;
  requester: string;
  amount: number;
  currency: string;
  status: string;
  createdAtUtc: string;
}

export interface SeedResult {
  requesterId: string;
  approverId: string;
  roleId: string;
  alreadySeeded: boolean;
}
