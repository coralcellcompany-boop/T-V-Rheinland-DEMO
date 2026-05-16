# Blue Sticker Inspection Workflow — Design Spec

**Date:** 2026-05-16
**Status:** Approved for planning
**Approach:** A — extend the existing patterns with a dedicated, fully separate Blue Sticker module + an isolated OTP service.

## 1. Goal

Implement the Blue Sticker inspection service as an exact 9-step workflow, and produce an
inspection report that is a byte-for-byte match of the official Aramco template
`Annex 1 - MS0053813 Aramco Inspection Report format.docx`, rendered to PDF.

The Blue Sticker module is **fully separate** from the existing TPI `InspectionCertificate`
aggregate. It persists **only** the fields that appear on the Annex 1 sheet — no checklists,
no photos, no findings, no field that is not on the sheet.

## 2. The 9-Step Flow (as agreed)

| Step | Action | Actor | Notes vs original list |
|------|--------|-------|------------------------|
| 1 | Create Job Order (Service = BlueSticker) + enter Org/RPO/CRM/Department | Coordinator | A `BlueStickerReport` is created per equipment, State = `Draft` |
| 2 | ~~Send OTP to client~~ | — | **Removed.** OTP is on-demand (step 7). Original step 2 is dropped per decision. |
| 3 | Go to site & start inspection | Inspector | `StartInspection` → State `InProgress`; **Inspection Date/Time stamped here** |
| 4 | Fill report data | Inspector | Area / Result / Deficiencies / Corrective / Receiver fields |
| 5 | Send report to Technical Reviewer | Inspector | `SubmitForReview` → State `UnderReview` |
| 6 | Approve | Technical Reviewer | `Approve` → State `Approved`. **Final approval** (Manager optional, can intervene). Stamps Reviewed date + reviewer name/signature. Auto-issues New Sticker No. |
| 7 | Request + enter Client OTP | Inspector @ site | `RequestClientOtp` emails a fresh OTP to the client; inspector enters it. State `AwaitingClientSignature` |
| 8 | Submit report | system (auto) | Auto-submit triggered by the client signature commit |
| 9 | Client signs on tablet | Client | Same screen as step 7. Commit → State `ClientSigned` (terminal); stamps Received date + receiver signature; generates signed Annex 1 PDF |

### State machine

States: `Draft → InProgress → UnderReview → Approved → AwaitingClientSignature → ClientSigned`
Plus: `Rejected` (Reviewer rejects → returns to `InProgress`), `Voided` (terminal).

Triggers: `StartInspection`, `SubmitForReview`, `Approve`, `Reject`, `RequestClientOtp`,
`VerifyOtpAndSign`, `Void`.

Implemented with the Stateless library, mirroring `CertificateStateMachine` patterns.
Every transition recorded with actor user id, role, UTC timestamp, optional comment.

## 3. Data Model — exact Annex 1 field set

New aggregate `BlueStickerReport` (own table, own EF configuration), **separate from
`InspectionCertificate`**. Fields = exactly the Annex 1 sheet. Auto vs. manual:

| # | Sheet field | Source | Auto/Manual | Entered by |
|---|-------------|--------|-------------|------------|
| 1 | TUV Job Order No. | `JobOrder.JobOrderNo` | AUTO | — |
| 2 | Aramco Category No. | `Equipment.AramcoCategory` | AUTO | — |
| 3 | Org. Code | admin | MANUAL | Coordinator @ Job Order |
| 4 | RPO No. | admin | MANUAL | Coordinator @ Job Order |
| 5 | CRM No. | admin | MANUAL | Coordinator @ Job Order |
| 6 | Report No. | system sequence | AUTO | — |
| 7 | Department / Contractor | admin | MANUAL | Coordinator @ Job Order |
| 8 | Inspection Date | day inspection starts | AUTO (at `StartInspection`) | — |
| 9 | Inspection Time | time inspection starts | AUTO (at `StartInspection`) | — |
| 10 | Previous Sticker No. | last issued sticker for equipment | AUTO (if any) | — |
| 11 | Previous Sticker Issued By | previous sticker's inspector | AUTO (if any) | — |
| 12 | Area of Inspection | — | MANUAL | Inspector @ site |
| 13 | Equipment ID No. | `Equipment.IdNo` | AUTO | — |
| 14 | Capacity | `Equipment.Swl` | AUTO | — |
| 15 | Equipment Location | `Equipment.Location` | AUTO (editable) | Inspector |
| 16 | Inspection Result | PASS / FAIL | MANUAL | Inspector @ site |
| 17 | New Sticker No. | sticker pool, auto-issued on `Approve` | AUTO | — |
| 18 | Manufacturer | `Equipment.Manufacturer` | AUTO | — |
| 19 | Model | `Equipment.Model` | AUTO | — |
| 20 | Equipment Type | `Equipment.EquipmentType` | AUTO | — |
| 21 | Equipment Serial No. | `Equipment.SerialNo` | AUTO | — |
| 22 | Sticker Expiration Date | Inspection Date + validity per Aramco category | AUTO | — |
| 23 | Deficiencies / Observations | — | MANUAL | Inspector @ site |
| 24 | Corrective Action Taken | — | MANUAL | Inspector @ site |
| 25–27 | Receiver: Name / Badge No. / Telephone | client rep on site | MANUAL | Inspector / client @ site |
| 28–30 | Inspector: Name / SAP No. / Telephone | inspector profile (identity) | AUTO | — |
| 31 | Technical Reviewer: Name | approving reviewer | AUTO | — |
| 32 | Received date | client signature date | AUTO | — |
| 33 | Reviewed date | tech reviewer approval date | AUTO | — |
| 34 | Receiver Signature | client signs on tablet | CAPTURED | client |
| 35 | Inspector Signature | inspector signs on tablet | CAPTURED | inspector |
| 36 | Technical Reviewer Signature | reviewer signs at approval | CAPTURED | reviewer |

All dates are AUTO. Manual data entry is limited to: Org Code, RPO, CRM,
Department/Contractor (Coordinator); Area of Inspection, Inspection Result,
Deficiencies, Corrective Actions, Receiver details (Inspector @ site).

## 4. OTP Service (isolated)

- Interface `IOtpService`; implementation `EmailOtpService` using the existing SMTP/Outbox
  pipeline (MailHog in dev).
- `RequestClientOtp`: generate a 6-digit code, store a hash + expiry on the report, email it
  to the client's address (resolved via `JobOrder.ClientId` → `Client`).
- Verification: hash comparison, single-use, attempt counter (max 5 → must resend),
  "resend" generates a fresh code and invalidates the previous one.
- Built as an abstraction so an `SmsOtpService` can be added later without rework.
- **Dependency to verify in plan:** `Client` entity must expose an email address. If absent,
  add it to `Client` or capture it on the Job Order.

## 5. On-site Finalize Screen (tablet)

Single Angular route `blue-sticker/finalize/:id`, tablet-first (large touch targets):
1. Read-only report summary (inspector reviews with client).
2. Receiver fields (Name / Badge No. / Telephone), filled on site.
3. "Send OTP to client" button → `RequestClientOtp`.
4. OTP input (inspector types the code the client received by email).
5. Verify → unlocks the signature pad (reuse existing `signature-pad.component`).
6. Client signs → commit → auto-submit → success screen + link to the PDF.

## 6. PDF Generation (Annex 1)

- Reuse the embedded `Annex1.docx` template (already byte-identical to the reference) and the
  existing `GotenbergClient`.
- New `BlueStickerReportTemplateFiller` (separate from the certificate's `Annex1TemplateFiller`)
  maps `BlueStickerReport` fields onto the docx cells, including the 3 signature cells.
- Keep the QuestPDF fallback path for when Gotenberg is unreachable.

## 7. Out of Scope / Removed

- TPI `InspectionCertificate` flow is untouched.
- No checklists / photos / findings anywhere in the Blue Sticker path.
- Original step 2 (OTP at Job Order creation) is removed; OTP is on-demand at step 7.
- Manager `FinalApprove` is not in the Blue Sticker path (Manager is optional/observational).
- The old Aramco form on the certificate detail page is not used for Blue Sticker; new
  inspections use the new module.

## 8. Testing

- Unit: state machine (every allowed + forbidden transition); `OtpService`
  (generate / verify / expiry / attempt lockout / resend); AUTO date computations
  (Sticker Expiration by Aramco category).
- Integration: full 9-step flow end-to-end; PDF generation (Gotenberg up + fallback).
- PDF mapping: snapshot test asserting the docx cells are filled with the expected values.

## 9. Open Dependencies (resolve during planning)

- Client email source for OTP delivery (see §4).
- Sticker Expiration validity period per Aramco category — confirm the mapping table source
  (likely the SIAC criteria / Aramco category reference docs).
- Inspector telephone field on the identity/profile model (Name + SAP No. already available
  via existing inspector-context handler).
