# Certification Workflow Source of Truth

## Status and authority

This document is the repository source of truth for backend and frontend certification behavior.

Its rules are based on the client answers in `Pertanyaan sistem e-sertifikasi (1).xlsx` and subsequent confirmed clarifications. When sources conflict, use this priority:

1. Later written client clarification.
2. The client-answer spreadsheet.
3. This document's explicitly marked interim rules.
4. Existing application behavior.

Do not treat an unresolved question as a confirmed business rule. Implement unresolved behavior only when this document supplies an interim rule, and keep it replaceable.

## Terminology

- **Association** is the RSPO-registered organization and certificate owner.
- **Poktan** belongs to an Association.
- **Petani** belongs to a Poktan and is Association master data.
- **Lahan** belongs to a Petani and is Association master data.
- **Baseline readiness** is persistent readiness attached to master Lahan, not a cycle phase.
- **Certification cycle** is Initial Certification, Surveillance 1-4, or Recertification.
- **Disclosure** is the explicit Petani/Lahan scope proposed and finalized for one cycle.
- Document verification and training run in parallel throughout the active cycle. Monitoring is a continuous Association activity and is not owned by a certification cycle.
- **Global Admin** is represented by the backend `SuperAdmin` role.

## Confirmed business rules

### Organization and scheduling

- A new Association immediately receives an active Initial Certification cycle.
- Initial Certification has sequence `0`; Surveillance uses `1` through `4`; the next cycle is Recertification.
- Surveillance preparation begins after the preceding certificate is issued.
- Its audit normally occurs 8-12 months after that certificate's issue date, based on readiness rather than calendar year.
- Only one current cycle may exist for an Association.
- Completed cycles remain immutable historical records.

### Master Petani and Lahan

- Petani and Lahan are created and updated from their master-data pages.
- Creating master data does not itself include it in a certification cycle.
- Lahan boundary geometry is created and updated from the Lahan page.
- There is no Registration page or certification phase.
- There is no Land Mapping page or certification phase.
- Cycle-specific `CertificationParticipant` and `CertificationParticipantLahan` records remain necessary to preserve participation history.

### Baseline readiness

- Baseline Assessment shows all existing Lahan accessible within an Association, regardless of the current cycle.
- Baseline assessments belong directly to master Lahan.
- Current check types are NKT, RTRW, Gambut, Status Kawasan, Status Konsesi, PIPPIP, and Perairan.
- A Lahan is certified only after it appears in `CertificateLahan` for an issued certificate.
- A certified Lahan does not repeat Baseline Assessment in later surveillance cycles.
- A never-certified Lahan needs valid boundary geometry and all required Baseline checks completed before inclusion in a finalized Disclosure.
- Passing Baseline never automatically includes a Lahan; Disclosure requires explicit selection.
- Until pass/fail rules are supplied, `ProgressStatus.Completed` means only that a check is complete. It must not be interpreted as regulatory approval.

Frontend readiness states are:

- `Certified`: previously listed on an issued certificate; no repeat Baseline required.
- `Complete`: never certified, boundary available, and all configured checks completed.
- `Incomplete`: some checks exist but boundary or required checks are incomplete.
- `NotAssessed`: no checks recorded.

### Disclosure

- Every Initial Certification, Surveillance, and Recertification cycle requires a new Disclosure.
- Previously certified Petani/Lahan are not carried over automatically.
- Association Admin explicitly selects and reconfirms scope every cycle.
- A Petani may have some Lahan included and others excluded.
- A finalized Disclosure requires at least one included Petani and Lahan, and every included Petani needs an included Lahan.
- Every candidate must have a resolved decision; `Selected`, `Confirmed`, and `Pending` block finalization.
- New Lahan must satisfy Baseline readiness. Certified Lahan bypass repeat Baseline.
- Draft Disclosure detail represents live working scope.
- Completion writes immutable `DisclosureParticipant` and `DisclosureLahan` snapshots.
- Preparation, audits, and certificates use the latest completed Disclosure snapshot as authoritative scope.

Petani cycle statuses:

- `Selected`: nominated but unresolved.
- `Confirmed`: confirmed but without a final inclusion decision; still unresolved.
- `Included`: proposed for inclusion.
- `Excluded`: administratively excluded; reason required.
- `Withdrawn`: withdrawn; reason required.

Lahan cycle statuses:

- `Pending`: unresolved.
- `Included`: proposed for inclusion.
- `Excluded`: excluded; reason required.
- `Withdrawn`: withdrawn; reason required.

Petani/Lahan may be added until Disclosure completes. Later scope changes work as follows:

1. Association Admin requests another Disclosure with a reason.
2. Global Admin approves or rejects it.
3. Only an approved additional Disclosure may mutate scope.
4. It must finish before Internal Audit.
5. Older completed versions remain immutable.

The backend currently permits version 2 and later through the same approval process. Whether the client intends exactly two versions remains unresolved.

### Preparation and documents

- Preparation begins after Disclosure completion.
- Document verification and training are parallel cycle activities; monitoring continues independently of cycles.
- Requirements may belong to the Association, cycle, participant, or participant Lahan.
- Participant/Lahan document screens and readiness use the latest completed Disclosure scope.
- Records outside that snapshot do not block preparation.
- Internal Audit does not wait for training attendance, monitoring, or required-document completion.
- Association Admin explicitly triggers Internal Audit when ready.
- Configurable training targets are deferred; document reuse follows the existing master-document attachment rules.

### Audits

- Internal Audit precedes External Audit.
- Each audit has one Association-level result, not separate Petani/Poktan/Lahan results.
- Findings and corrective actions are stored separately.
- Assigned ICS Auditors create/update findings for their Poktan. Assigned Poktan Admins close submitted findings; Association Admins may close any finding in their Association.
- Internal findings support web entry and XLSX template/preview/confirmed bulk import. Duplicate codes are rejected and imports never overwrite existing findings.
- Internal Audit completes only when it and all findings are closed.
- Internal findings must close before External Audit.
- The External Audit report is a required PDF. Uploading it records the audit as performed and declares whether findings exist.
- With no findings, report upload closes External Audit automatically. With findings, the closure-report PDF is due within three months and closes the audit automatically.
- External Audit cannot progress through separate perform/close actions; its state is driven by the two document routes.
- Association Admin manages assigned-Association transitions, audit results, and certificate upload.
- Global Admin approval is specifically required for reopening Disclosure scope.

### Certificates and rollover

- The certificate is issued to the Association and contains its official Petani/Lahan scope.
- Every surveillance produces a new certificate.
- Certificate participant/Lahan rows are immutable snapshots.
- Certificate issuance completes the current cycle and creates the next current cycle.
- The new cycle starts at `Disclosure` and requires explicit reconfirmation.
- Progress reaches 100% only after certificate issuance and file upload.

## Certification lifecycle

```text
Disclosure
    ↓
Preparation
├── Document Verification
├── Training
└── Monitoring
    ↓
Internal Audit
    ↓
External Audit
    ↓
Certificate Issuance
    ↓
Completed / next cycle created
```

Valid `CertificationPhase` values:

1. `Disclosure`
2. `Preparation`
3. `InternalAudit`
4. `ExternalAudit`
5. `CertificateIssuance`
6. `Completed`

Valid progress steps:

- `Disclosure`
- `Training`
- `DatabaseAndDocumentManagement`
- `Monitoring`
- `InternalAudit`
- `ExternalAudit`
- `CertificateIssuance`

Baseline is absent from cycle progress because it belongs to master Lahan and certified Lahan do not repeat it.

## Frontend page responsibilities

### Petani

- Create/update master Petani and show Poktan/Association ownership.
- Do not equate creation with certification enrollment.
- Show current cycle participation when available.

### Lahan

- Create/update master Lahan and boundary geometry.
- Show certified state, Baseline readiness, and completed checks.
- Link to Baseline detail.
- Never call removed cycle Land Mapping endpoints.

### Baseline Assessment

- Load every Association Lahan, not only cycle participants.
- Distinguish `Certified`, `Complete`, `Incomplete`, and `NotAssessed`.
- Treat certified Lahan as read-only for repeat assessment.
- Show missing boundary as a blocker.
- Remain available independently of cycle phase.

### Disclosure

- Create/open the active draft and list all Association candidates.
- Show Baseline readiness, blocker codes, and current-cycle identifiers.
- Explicitly include/exclude Petani/Lahan.
- Enroll a Petani when first selected and add later Lahan to an existing participant.
- Finalize only after all decisions and Baseline prerequisites are satisfied.
- Show completed versions as immutable snapshots.
- After completion, expose the additional-Disclosure request and Global Admin review state.

### Verifikasi Dokumen

- Operate on the latest completed Disclosure scope.
- Separate Association, cycle, Petani, and Lahan requirements.
- Do not display excluded draft candidates as required owners.

### Training and Monitoring

- Training is cycle-scoped and may have multiple sessions in a cycle.
- Training sessions may be created, have attendance managed, be completed, or be reopened while the cycle is current and active, regardless of its current phase.
- A training attendee is a master `Petani`, not a `CertificationParticipant`. Any active Petani in a Poktan belonging to the cycle's Association may be invited even when that Petani is not in the certification scope.
- `TrainingSession.CompletedAt` finalizes the attendance sheet. It does not mean that every attendee has completed a certificate requirement.
- There is currently no automatic attendance threshold or per-Petani training-completion flag. The certificate-eligibility workspace shows the sessions and attendance status for each included Petani, and the Association Admin makes the qualification decision.
- Cycle training routes use `petaniIds` and `petaniId`, not certification participant IDs:
  - `POST /api/certification/cycles/{cycleId}/training`
  - `GET /api/certification/cycles/{cycleId}/training`
  - `PUT /api/certification/training/{sessionId}/attendance/{petaniId}`
  - `POST /api/certification/training/{sessionId}/complete`
  - `POST /api/certification/training/{sessionId}/reopen`
- Monitoring is a continuous Poktan/Sub-ICS activity across certification-cycle boundaries and is never a readiness blocker or part of the certification percentage.
- The 13 MICS forms have independent typed entities beneath a shared submission lifecycle. See `docs/monitoring.md`.
- Annual and semiannual submissions use explicit reporting-period start/end dates; observations retain their actual dates.
- Monitoring routes are:
  - `POST /api/certification/monitoring`
  - `PUT /api/certification/monitoring/{id}`
  - `DELETE /api/certification/monitoring/{id}`
  - `GET /api/certification/monitoring?associationId={id}&poktanId=&petaniId=&year=&month=`
  - `GET /api/certification/cycles/{cycleId}/monitoring` remains an Association-filtered compatibility view; records are not owned by that cycle.
- The routes above are legacy compatibility routes. New integrations use `/api/monitoring`; `Budidaya`/`Ics` are retained only for legacy/grouping compatibility.

### Certificate eligibility and issuance

- Required documents and training do not block the transition to Internal Audit. Document upload, attachment, replacement, submission, and verification remain available throughout the current active cycle.
- Before certificate issuance, every included `CertificationParticipant` must receive an explicit `Qualified` or `Excluded` eligibility decision. `Pending` is not a final decision.
- Use `GET /api/certification/cycles/{cycleId}/certificate-eligibility` for the frontend review workspace. Each row includes Petani/Poktan identity, decision audit fields, cycle training attendance, and `missingRequiredDocumentCount`.
- Use `PUT /api/certification/participants/{participantId}/certificate-eligibility` with `{ status, reason }`. `Excluded` requires a reason. `Qualified` is rejected while a required Petani or included-Lahan document is unverified.
- The decision stores the deciding user and timestamp. Changing participation scope resets the decision to `Pending`.
- Issuance also requires all Association-level required documents to be verified and rechecks every qualified Petani/Lahan document server-side.
- Only `Qualified` Petani and their included Lahan are copied into the immutable certificate snapshot. A Petani marked `Excluded` and all of that Petani's Lahan are omitted without blocking the other qualified recipients.
- The future configurable rule for required training sessions/counts is intentionally deferred. For now, attendance is evidence and qualification is an Association Admin judgment.

### Audits

- Disclosure, document verification, Internal Audit, and External Audit are concurrent workspaces on an active current cycle; the frontend does not advance between them.
- Internal and External Audit may be created and worked independently. Their completion is validated when the certificate is issued.
- Show one Association-level audit per type and cycle.
- For Internal Audit, manage findings and closure separately and offer XLSX template, preview, and confirmed import.
- For External Audit, upload the audit PDF and indicate whether it contains findings; show the closure PDF control only when findings exist.
- Block certificate issuance until both audits and their findings satisfy the completion rules.

### Certificate

- Upload Association certificate file and metadata.
- Display immutable certificate Petani/Lahan scope.
- Treat historical certificates as read-only.

## Current API contract

### Master Lahan and Baseline

- `GET /api/lahan?associationId={associationId}` — paged master Lahan.
- `GET /api/lahan/{lahanId}` — detail including boundary.
- `POST /api/lahan` — create Lahan and boundary.
- `PUT /api/lahan/{lahanId}` — update Lahan and boundary.
- `GET /api/lahan/{lahanId}/baseline` — master-Lahan checks.
- `PUT /api/lahan/{lahanId}/baseline` — update one check; conflicts for certified Lahan or missing boundary.
- `GET /api/associations/{associationId}/baseline-readiness` — all Association Lahan readiness.

### Cycle and Disclosure

- `GET /api/associations/{associationId}/certification-cycles/current` — current cycle, progress, permissions, pages.
- `POST /api/certification/cycles/{cycleId}/disclosures` — create first draft or request another version.
- `GET /api/certification/cycles/{cycleId}/disclosures` — version list.
- `GET /api/certification/disclosures/{disclosureId}` — live draft or final snapshot.
- `GET /api/certification/disclosures/{disclosureId}/candidates` — all candidates with readiness/current-cycle IDs.
- `POST /api/certification-cycles/{cycleId}/participants` — enroll Petani and initial Lahan.
- `POST /api/certification-cycles/participants/{participantId}/lahan` — add Lahan to an enrolled Petani.
- `POST /api/certification-cycles/participants/{participantId}/status` — Petani scope status.
- `PUT /api/certification/disclosures/{disclosureId}/lahan/{participantLahanId}` — Lahan inclusion/exclusion.
- `POST /api/certification/disclosures/{disclosureId}/complete` — validate and snapshot scope.
- `POST /api/certification/disclosures/{disclosureId}/review` — Global Admin review of another version.

Enrollment returns `participantId`; add-Lahan returns `participantLahanId` values. Empty `LahanIds` during initial enrollment currently means all Lahan owned by that Petani, so frontend must always send explicit IDs.

### Downstream workflow

- `GET /api/certification-cycles/{cycleId}/document-verification` — snapshot-scoped verification.
- `GET /api/certification-cycles/{cycleId}/document-readiness` — snapshot-scoped summary.
- `POST /api/certification-cycles/{cycleId}/transition` — sequential transition with readiness checks.
- Training, audit, finding, and certificate endpoints remain cycle-scoped. Monitoring uses master Petani/Lahan scope and offers an Association-filtered query.
- Internal finding bulk routes are `GET /api/certification/audits/{auditId}/findings/template`, `POST .../import/preview`, and `POST .../import`.
- External audit routes are `POST /api/certification/audits/{auditId}/report` (multipart fields `documentTypeId`, `file`, and required `hasFindings`) and `POST /api/certification/audits/{auditId}/closure-report`.

Important blocker/conflict codes include:

- `DISCLOSURE_INCOMPLETE`
- `PARTICIPANTS_UNCONFIRMED`
- `LAHAN_PENDING`
- `LAHAN_BOUNDARY_MISSING`
- `BASELINE_INCOMPLETE`
- `TRAINING_TARGET_NOT_CONFIGURED`
- `TRAINING_INCOMPLETE`
- `MONITORING_TARGET_NOT_CONFIGURED`
- `MONITORING_INCOMPLETE`
- document and audit-specific incomplete codes
- `CERTIFICATION_CYCLE_READ_ONLY`

## Authorization expectations

- `SuperAdmin`: all Associations/cycles and additional-Disclosure approval.
- `AssociationAdmin`: assigned master data and workflow, scope, transitions, audits, and certificates.
- `PoktanAdmin`: allowed master data/preparation within assigned Poktan; no Association certificate or final audit authority.
- `MemberTaniBaik`: read-only access to their own permitted data and participation.

Frontend visibility is not authorization. Always handle `403`, `409`, and structured backend blockers.

## Progress rules

- Progress is computed from activity records and is not directly editable.
- Applicable cycle steps are equally weighted.
- Participant completion uses completed applicable participants divided by applicable participants.
- Approved scope additions may reduce progress.
- Internal Audit reaches 100% only after all findings close.
- Performed but unresolved audits may receive configurable partial progress.
- The cycle reaches 100% only after certificate issuance/upload.

## Unresolved client questions

Do not silently answer these in frontend behavior.

### Baseline and Lahan

1. Can a completed Baseline for a never-certified, non-selected Lahan be reused later?
2. Which changes invalidate readiness: boundary, ownership, legal number, location, area, datasets, or methodology?
3. What are exact pass, warning, and fail results for each check?
4. Which failures may be overridden, by whom, and with what evidence?
5. Must boundary changes be versioned/reviewed, and must old geometry remain accessible?

Interim behavior: checks stay with Lahan; certified Lahan cannot be reassessed; geometry changes do not automatically invalidate checks.

### Disclosure and master-data changes

6. Is only one second Disclosure allowed, or can version 3+ be approved before Internal Audit?
7. What is the final business distinction between `Excluded` and `Withdrawn`, including future reselection?
8. Which master-data changes after Disclosure require another Disclosure?
9. Between Disclosure and certificate issuance, should certificates use Disclosure-time or latest master values?
10. Does certificate issuance lock snapshots of names, legal identifiers, area, and geometry as well as membership?

Interim behavior: completed membership is immutable, later approved versions may be created before Internal Audit, and certificate membership is snapshotted. Full field-level Disclosure snapshots are not defined.

### Documents, training, and monitoring

11. Which document types/counts are mandatory for each owner type?
12. Which verified documents may be reused across cycles, and with what expiry/change rules?
13. Who must attend which training, how many sessions are required, and what repeats during surveillance?
14. What monitoring frequency/categories apply, especially to participants added partway through a cycle?

Interim behavior: requirements and targets are cycle configuration; frontend renders backend values.

### Audits and progress

15. Must audit performer and approver be different users?
16. Who may create/close findings, approve results, or correct finalized audits?
17. Do missed finding deadlines warn, block transitions, or automatically fail an audit?
18. Can failed External Audit repeat in the same cycle or receive a deadline extension?
19. What partial-progress percentages apply to performed but unresolved audits?
20. What is the final detailed progress formula after targets are supplied?

Interim behavior: overdue findings are reported, open findings block required transitions, and no automatic cycle failure is inferred.

## Database migration note

The development migration must be regenerated because:

- `BaselineAssessment` references `LahanId` instead of `CertificationParticipantLahanId`.
- Registration, Land Mapping, and cycle Baseline enum members were removed.
- New cycles start at `Disclosure`.

No migration was generated during this refactor. Regenerate it before using the updated model with a persistent database.
