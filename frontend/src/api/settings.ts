import type { Api } from "./client";

export interface RoleDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
}

export interface CreateRoleInput {
  code: string;
  name: string;
  description?: string;
}

export interface WorkflowRuleInput {
  attribute: string;
  operator: string;
  value: string;
  conjunction: string;
}

export interface WorkflowStepInput {
  stepName: string;
  sequence: number;
  approvalRoleId: string;
  isMandatory: boolean;
  rules: WorkflowRuleInput[];
}

export interface WorkflowRuleDto extends WorkflowRuleInput {
  id: string;
}

export interface WorkflowStepDto {
  id: string;
  stepName: string;
  sequence: number;
  approvalRoleId: string;
  approvalRoleName: string;
  isMandatory: boolean;
  rules: WorkflowRuleDto[];
}

export interface WorkflowVersionDto {
  id: string;
  versionNumber: number;
  status: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  steps: WorkflowStepDto[];
}

export interface WorkflowDefinitionDto {
  id: string;
  name: string;
  entityType: string;
  description: string | null;
  status: string;
  versions: WorkflowVersionDto[];
}

export interface CreateWorkflowDefinitionInput {
  name: string;
  entityType: string;
  description?: string;
  steps: WorkflowStepInput[];
}

export const RULE_OPERATORS = ["Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterOrEqual", "LessOrEqual"] as const;
export const KNOWN_ENTITY_TYPES = ["PurchaseRequisition", "PurchaseOrder", "GoodsReceipt"] as const;
export const FIELD_DATA_TYPES = ["Text", "Number", "Date", "Boolean", "Select"] as const;
export type FieldDataType = (typeof FIELD_DATA_TYPES)[number];

export interface FieldDefinitionDto {
  id: string;
  entityType: string;
  fieldKey: string;
  label: string;
  dataType: FieldDataType;
  isRequired: boolean;
  selectOptions: string[] | null;
  dependsOnFieldKey: string | null;
  dependsOnValue: string | null;
  sequence: number;
  isActive: boolean;
}

export interface CreateFieldDefinitionInput {
  entityType: string;
  fieldKey: string;
  label: string;
  dataType: FieldDataType;
  isRequired: boolean;
  selectOptions?: string[];
  dependsOnFieldKey?: string;
  dependsOnValue?: string;
  sequence: number;
}

export interface UpdateFieldDefinitionInput {
  label: string;
  dataType: FieldDataType;
  isRequired: boolean;
  selectOptions?: string[];
  dependsOnFieldKey?: string;
  dependsOnValue?: string;
  sequence: number;
}

export const listFields = (api: Api, entityType?: string) =>
  api.get<FieldDefinitionDto[]>(`/api/v1/admin/fields${entityType ? `?entityType=${entityType}` : ""}`);
export const createField = (api: Api, body: CreateFieldDefinitionInput) => api.post<{ id: string }>("/api/v1/admin/fields", body);
export const updateField = (api: Api, id: string, body: UpdateFieldDefinitionInput) => api.put<void>(`/api/v1/admin/fields/${id}`, body);
export const deactivateField = (api: Api, id: string) => api.post<void>(`/api/v1/admin/fields/${id}/deactivate`);

export const listRoles = (api: Api) => api.get<RoleDto[]>("/api/v1/admin/roles");
export const createRole = (api: Api, body: CreateRoleInput) => api.post<{ id: string }>("/api/v1/admin/roles", body);

export const listWorkflows = (api: Api) => api.get<WorkflowDefinitionDto[]>("/api/v1/admin/workflows");
export const createWorkflowDefinition = (api: Api, body: CreateWorkflowDefinitionInput) =>
  api.post<{ id: string }>("/api/v1/admin/workflows", body);
export const createWorkflowVersion = (api: Api, definitionId: string, steps: WorkflowStepInput[]) =>
  api.post<{ id: string }>(`/api/v1/admin/workflows/${definitionId}/versions`, { steps });
