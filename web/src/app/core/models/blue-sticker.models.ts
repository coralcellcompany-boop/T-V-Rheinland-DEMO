export enum BlueStickerState {
  Draft = 0, InProgress = 1, UnderReview = 2, Approved = 3,
  AwaitingClientSignature = 4, ClientSigned = 5, Rejected = 6, Voided = 7,
}
export const BlueStickerStateName: Record<number, string> = {
  0: 'Draft', 1: 'In progress', 2: 'Under review', 3: 'Approved',
  4: 'Awaiting client signature', 5: 'Client signed', 6: 'Rejected', 7: 'Voided',
};
export enum BlueStickerResult { NotSet = 0, Pass = 1, Fail = 2 }

export interface CreateBlueStickerReportsRequest {
  jobOrderId: string;
  orgCode?: string | null;
  rpoNo?: string | null;
  crmNo?: string | null;
  departmentContractor?: string | null;
}
export interface UpdateBlueStickerInspectionRequest {
  areaOfInspection?: string | null;
  result: BlueStickerResult;
  deficiencies?: string | null;
  correctiveActionsTaken?: string | null;
  equipmentLocation?: string | null;
  receiverName?: string | null;
  receiverBadgeNo?: string | null;
  receiverTelephone?: string | null;
  inspectorTelephone?: string | null;
}
export interface BlueStickerTransitionDto {
  fromState: string;
  toState: string;
  actorUserId: string;
  actorRole: string;
  comments?: string | null;
  atUtc: string;
}
export interface BlueStickerReportDetail {
  id: string; reportNo: string; jobOrderId: string; equipmentId: string;
  clientId: string;
  tuvJobOrderNo: string; aramcoCategoryNo?: string | null;
  orgCode?: string | null; rpoNo?: string | null; crmNo?: string | null;
  departmentContractor?: string | null;
  inspectionDate?: string | null; inspectionTime?: string | null;
  previousStickerNo?: string | null; previousStickerIssuedBy?: string | null;
  areaOfInspection?: string | null; result: BlueStickerResult;
  equipmentIdNo: string; capacity?: string | null; equipmentLocation?: string | null;
  manufacturer?: string | null; model?: string | null; equipmentType?: string | null;
  equipmentSerialNo?: string | null; newStickerNo?: string | null;
  stickerExpirationDate?: string | null;
  deficiencies?: string | null; correctiveActionsTaken?: string | null;
  receiverName?: string | null; receiverBadgeNo?: string | null;
  receiverTelephone?: string | null; inspectorName?: string | null;
  inspectorSapNo?: string | null; inspectorTelephone?: string | null;
  technicalReviewerName?: string | null;
  receivedDate?: string | null; reviewedDate?: string | null;
  receiverSignaturePng?: string | null; inspectorSignaturePng?: string | null;
  technicalReviewerSignaturePng?: string | null;
  state: BlueStickerState; createdAtUtc: string;
  updatedAtUtc?: string | null;
  transitions: BlueStickerTransitionDto[];
}
export interface BlueStickerReportListItem {
  id: string; reportNo: string; tuvJobOrderNo: string; equipmentIdNo: string;
  state: BlueStickerState; inspectionDate?: string | null; createdAtUtc: string;
}
export type BlueStickerTrigger =
  'StartInspection' | 'SubmitForReview' | 'Approve' | 'Reject' | 'Void';
