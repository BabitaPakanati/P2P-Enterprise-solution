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
  customFields: Record<string, string>;
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
  customFields?: Record<string, string>;
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
  customFields: Record<string, string>;
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
  customFields?: Record<string, string>;
}

export interface AmendPurchaseOrderInput {
  supplierName: string;
  deliveryDate?: string;
  changeReason: string;
  lines: CreateOrderLineInput[];
  customFields?: Record<string, string>;
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

export interface ReceivableLine {
  purchaseOrderLineId: string;
  itemDescription: string;
  uom: string;
  quantityOrdered: number;
  quantityAlreadyReceived: number;
  quantityRemaining: number;
}

export interface PurchaseOrderReceiptStatus {
  receiptStatus: string;
  lines: ReceivableLine[];
}

export interface GoodsReceiptLine {
  id: string;
  purchaseOrderLineId: string;
  itemDescription: string;
  uom: string;
  quantityOrdered: number;
  quantityReceived: number;
  quantityAccepted: number;
  quantityRejected: number;
  inspectionStatus: string;
}

export interface GoodsReceiptSummary {
  id: string;
  receiptNumber: string;
  purchaseOrderId: string;
  poNumber: string;
  supplierName: string;
  deliveryDate: string;
  status: string;
}

export interface GoodsReceiptDetail extends GoodsReceiptSummary {
  documentId: string;
  deliveryNoteNumber: string | null;
  location: string | null;
  currentVersionNumber: number;
  lines: GoodsReceiptLine[];
  customFields: Record<string, string>;
}

export interface CreateGoodsReceiptLineInput {
  purchaseOrderLineId: string;
  quantityReceived: number;
  quantityRejected: number;
}

export interface CreateGoodsReceiptInput {
  purchaseOrderId: string;
  deliveryDate: string;
  deliveryNoteNumber?: string;
  location?: string;
  lines: CreateGoodsReceiptLineInput[];
  customFields?: Record<string, string>;
}

/** Same shape as create minus purchaseOrderId (fixed once created) - editing a Draft replaces the whole thing. */
export interface UpdateGoodsReceiptInput {
  deliveryDate: string;
  deliveryNoteNumber?: string;
  location?: string;
  lines: CreateGoodsReceiptLineInput[];
  customFields?: Record<string, string>;
}

export interface AmendGoodsReceiptInput {
  deliveryDate: string;
  deliveryNoteNumber?: string;
  location?: string;
  changeReason: string;
  lines: CreateGoodsReceiptLineInput[];
  customFields?: Record<string, string>;
}

export interface SeedResult {
  requesterId: string;
  approverId: string;
  roleId: string;
  alreadySeeded: boolean;
}
