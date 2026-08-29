import type { Api } from "./client";
import type {
  GoodsReceiptSummary, GoodsReceiptDetail, CreateGoodsReceiptInput, UpdateGoodsReceiptInput, AmendGoodsReceiptInput,
  PurchaseOrderReceiptStatus, DocumentVersion,
} from "./types";

export const listGoodsReceipts = (api: Api, purchaseOrderId?: string) =>
  api.get<GoodsReceiptSummary[]>(`/api/v1/goods-receipts${purchaseOrderId ? `?purchaseOrderId=${purchaseOrderId}` : ""}`);
export const getGoodsReceipt = (api: Api, id: string) => api.get<GoodsReceiptDetail>(`/api/v1/goods-receipts/${id}`);
export const getGoodsReceiptVersions = (api: Api, id: string) => api.get<DocumentVersion[]>(`/api/v1/goods-receipts/${id}/versions`);
export const createGoodsReceipt = (api: Api, body: CreateGoodsReceiptInput) =>
  api.post<{ id: string }>("/api/v1/goods-receipts", body);
export const updateGoodsReceipt = (api: Api, id: string, body: UpdateGoodsReceiptInput) =>
  api.put<void>(`/api/v1/goods-receipts/${id}`, body);
export const postGoodsReceipt = (api: Api, id: string) => api.post<void>(`/api/v1/goods-receipts/${id}/post`);
export const cancelGoodsReceipt = (api: Api, id: string) => api.post<void>(`/api/v1/goods-receipts/${id}/cancel`);
export const amendGoodsReceipt = (api: Api, id: string, body: AmendGoodsReceiptInput) =>
  api.post<void>(`/api/v1/goods-receipts/${id}/amend`, body);

export const getPurchaseOrderReceiptStatus = (api: Api, purchaseOrderId: string) =>
  api.get<PurchaseOrderReceiptStatus>(`/api/v1/purchase-orders/${purchaseOrderId}/receipt-status`);
