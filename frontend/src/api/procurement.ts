import type { Api } from "./client";
import type {
  RequisitionSummary, RequisitionDetail, CreateRequisitionInput, UpdateRequisitionInput, AmendRequisitionInput,
  OrderSummary, OrderDetail, CreatePurchaseOrderInput, AmendPurchaseOrderInput, DocumentVersion,
  ApprovalTask, SeedResult,
} from "./types";

export const seedFoundation = (api: Api) => api.post<SeedResult>("/api/v1/_diagnostics/seed-foundation");

export const listRequisitions = (api: Api, mine: boolean) =>
  api.get<RequisitionSummary[]>(`/api/v1/requisitions?mine=${mine}`);
export const getRequisition = (api: Api, id: string) => api.get<RequisitionDetail>(`/api/v1/requisitions/${id}`);
export const getRequisitionVersions = (api: Api, id: string) => api.get<DocumentVersion[]>(`/api/v1/requisitions/${id}/versions`);
export const createRequisition = (api: Api, body: CreateRequisitionInput) =>
  api.post<{ id: string }>("/api/v1/requisitions", body);
export const updateRequisition = (api: Api, id: string, body: UpdateRequisitionInput) =>
  api.put<void>(`/api/v1/requisitions/${id}`, body);
export const submitRequisition = (api: Api, id: string) => api.post<void>(`/api/v1/requisitions/${id}/submit`);
export const cancelRequisition = (api: Api, id: string) => api.post<void>(`/api/v1/requisitions/${id}/cancel`);
export const amendRequisition = (api: Api, id: string, body: AmendRequisitionInput) =>
  api.post<void>(`/api/v1/requisitions/${id}/amend`, body);

export const listOrders = (api: Api) => api.get<OrderSummary[]>("/api/v1/purchase-orders");
export const getOrder = (api: Api, id: string) => api.get<OrderDetail>(`/api/v1/purchase-orders/${id}`);
export const getOrderVersions = (api: Api, id: string) => api.get<DocumentVersion[]>(`/api/v1/purchase-orders/${id}/versions`);
export const createOrder = (api: Api, body: CreatePurchaseOrderInput) =>
  api.post<{ id: string }>("/api/v1/purchase-orders", body);
export const submitOrder = (api: Api, id: string) => api.post<void>(`/api/v1/purchase-orders/${id}/submit`);
export const sendOrder = (api: Api, id: string) => api.post<void>(`/api/v1/purchase-orders/${id}/send`);
export const amendOrder = (api: Api, id: string, body: AmendPurchaseOrderInput) =>
  api.post<void>(`/api/v1/purchase-orders/${id}/amend`, body);

export const myApprovals = (api: Api) => api.get<ApprovalTask[]>("/api/v1/approvals/my");
export const decideApproval = (api: Api, taskId: string, approve: boolean, comments?: string) =>
  api.post<void>(`/api/v1/approvals/${taskId}/decide`, { approve, comments });
