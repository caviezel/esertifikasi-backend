# Frontend integration: training, field logs, and MICS monitoring

This document describes the implemented API. JSON uses camelCase and string enum values. Dates are `YYYY-MM-DD`; timestamps are ISO 8601. All endpoints require the existing authenticated session/Bearer token. IDs are UUIDs. Money is rupiah, weights kg, material dosage kg/tree or liters/ha, and areas hectares. Never send calculated totals or historical snapshots.

Schema rollout is required first: [manual migration notes](operations-migration-notes.md). No migration was added.

## Association context and permissions

`GET /api/associations/{associationId}/operations/context`

Returns `association: {id,nama}`, optional `currentCycle`, optional certification `progress`, `canManageAssociationTraining`, `canReopen`, and `poktans: [{id,nama,canManage,canFinalize}]`. Use this for the association header and controls. A missing cycle does not disable training or monitoring. Current certification-cycle page metadata also keeps training/monitoring available across phases.

- Super Admin and Association Admin manage the association.
- Poktan Admin and assigned ICS auditors manage their scoped Poktan. When creating/editing training as these roles, supply `poktanId`; association-wide training is managed by Association Admin.
- Association/Poktan Admin finalize; ICS auditors cannot finalize.
- Association Admin/Super Admin reopen with a nonblank reason.
- Farmers can view their own training, attendance, and parcel observations. Responses filter participant lists/counts to the viewer's accessible scope.
- Anonymous/confidential complaints are managed by Association Admin/Super Admin. An identified farmer can view their own confidential complaint. Anonymous complaints have no displayed farmer identity.

Paged endpoints return `{items,page,pageSize,totalCount,totalPages}`. Query `page` defaults to 1, `pageSize` to 20 (max 100), `search` is optional. An empty authorized scope yields an empty list.

## Ongoing association training

Base: `/api/associations/{associationId}/training`.

| Method | Path | Purpose |
|---|---|---|
| GET | base | Paged cards; query `search`, `from`, `to`, `status`, `page`, `pageSize` |
| GET | `/summary` | Cards: `totalTraining`, unique `totalParticipants`, `completed`, `upcoming`, `ongoing`, `awaitingCompletion`; date/search filters apply |
| GET | `/candidates` | All association farmers in viewer scope; optional `poktanId`, search, pagination |
| POST | base | Create |
| GET | `/{id}` | Topics, selected days, participants and daily attendance |
| PUT | `/{id}` | Replace editable title, dates, selected days, topics, participants |
| PUT | `/{id}/attendance/{petaniId}/{date}` | Mark one participant on one selected day |
| POST | `/{id}/complete` | Finalize session after all participant/day cells are explicitly marked |
| POST | `/{id}/reopen` | Body `{"reason":"Correct attendance"}` |
| DELETE | `/{id}` | Soft delete an incomplete session with no recorded attendance |

Create/edit:

```json
{
  "title": "Pelatihan ISPO & GAP",
  "poktanId": null,
  "startDate": "2026-04-15",
  "endDate": "2026-04-17",
  "days": ["2026-04-15", "2026-04-17"],
  "petaniIds": ["<farmer-id>"],
  "topicIds": ["b6000000-0000-0000-0000-000000000001"],
  "packageIds": ["b6000000-0000-0000-0001-000000000001"]
}
```

Days must be unique and within the range; breaks/weekends are not automatically attendance days. Package topics are merged with explicit topics and deduplicated. At least one participant and one resulting topic are required. Fetch all candidate pages for select-all; do not assume the first 20 farmers are the entire association. All farmers are eligible independently of certification enrollment.

Status values: `Upcoming` (today before start), `Ongoing` (within date range), `AwaitingCompletion` (past end but not completed), `Completed` (explicitly completed). Today is resolved in `Asia/Makassar`. Completion cannot occur before the last selected day. Completed sessions require reopening before any edits.

Attendance request:

```json
{"present": true, "notes": ""}
```

A missing row means **unmarked**, not absent. Mark absence using `present: false`. Participant completion is true only if every selected day is present. Cards return `participantCount` and `completedParticipantCount`; use these for the completion bar. Detail returns `attendance: [{petaniId,date,present,notes}]` and `participants: [{petaniId,nama,completed}]`. Search the returned detail list locally. A participant or day with any attendance record (including explicit absence) cannot be removed. Topic IDs in GET responses can be sent back in PUT; package selection does not need to be reconstructed.

Existing copied sessions may expose `legacyDescription`, which should be displayed when structured topics have not yet been mapped.

## Topic packages and P3K locations

Base: `/api/operations/catalog`.

| Method | Path | Request/scope |
|---|---|---|
| GET | `/topics?associationId=...` | Global + association custom topics |
| POST | `/topics?associationId=...` | `{"name":"Custom topic"}`; Association Admin |
| POST | `/topics` | Shared topic; Super Admin |
| PUT | `/topics/{id}` | `{"name":"New name"}`; owner scope |
| GET | `/packages` | Shared packages with topic IDs |
| POST/PUT | `/packages[/{id}]` | `{"name":"Package","topicIds":["..."]}`; Super Admin; shared topics only |
| GET | `/first-aid-locations?associationId=...` | Active reusable locations |
| POST | `/first-aid-locations?associationId=...` | `{"name":"Kantor ICS"}`; Association Admin |
| PUT | `/first-aid-locations/{id}` | Rename; existing inspections retain historical location text |
| DELETE | `/first-aid-locations/{id}` | Deactivate |

Custom-topic creation is idempotent for an exact name within the association. Save a custom topic first, then include its returned ID in training. Initial topics are ISPO, RSPO, SKI/ICS, APD, K3; the initial package contains the first three.

## Farmer field logs

Base: `/api/field-logs`. Enum `activity`: `Harvest`, `Fertilizing`, `Spraying`, `Pruning`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/table` | Parcel rows, including parcels without logs; required `associationId`, `activity`, `year`; optional `poktanId`, `lahanId`, `rotationId`, `status`, search/pagination |
| GET | base | Paged individual records; same filters |
| POST | base | Create a draft record |
| GET/PUT/DELETE | `/{id}` | Detail/edit/soft delete |
| POST | `/{id}/finalize` | Confirm record |
| POST | `/{id}/reopen` | `{"reason":"Correction"}` |
| GET | `/totals?lahanId=...&activity=Harvest&year=2026` | Finalized totals overall and per rotation |
| GET | `/rotations?lahanId=...` | Parcel's rotations |
| PUT | `/rotations/{id}` | Edit name/dates; dates must still contain every linked active harvest record |
| POST | `/rotations` | `{"lahanId":"...","name":"Rotasi 1","startDate":"2026-01-01","endDate":"2026-06-30"}` |

Table items contain `land`, `recordCount`, `latestActivityDate`, and `records`. `land.noSertifikat` is the existing parcel legality number; retain the requested UI label **No Sertifikat**. `noLahan` is `NoPetaLahan` if present; always use `lahanId` as the API identity. Table area uses legal area in ha, not treated area.

Each record belongs to one parcel. Harvest rotations are explicit per parcel, cannot overlap (inclusive boundaries), and must contain the harvest date. No fixed 4–6 month duration is enforced; that is the current operating convention.

Every activity gets an automatic sequence number per parcel/type/year. Pupuk/Semprot/Pruning restart at 1 annually; deleted numbers are not reused. Multiple records on the same date are allowed. Edits cannot change parcel, activity type, or calendar year; recreate a mistaken record instead.

All four requests share `lahanId`, `activity`, `activityDate`, optional `notes`. Only populate fields relevant to the selected type:

| Activity | Additional fields |
|---|---|
| Harvest | `rotationId`, `buyer`, `weightKg`, `unitPrice` (Rp/kg), `bunchCount`, `deductions`, `transport`, `workerCount`, `wagePerPerson` |
| Fertilizing | `materialName`, `unitPrice` (Rp/kg), `treeCount`, `dosePerTreeKg`, `workerCount`, `wagePerPerson` |
| Spraying | `materialName`, `unitPrice` (Rp/liter), `treatedAreaHa`, `doseLitersPerHa`, `workerCount`, `wagePerPerson` |
| Pruning | `treeCount`, `wagePerTree` |

Example harvest:

```json
{
  "lahanId": "<parcel-id>",
  "activity": "Harvest",
  "activityDate": "2026-02-15",
  "rotationId": "<rotation-id>",
  "buyer": "PKS Agro Maju",
  "weightKg": 1000,
  "unitPrice": 2500,
  "bunchCount": 50,
  "deductions": 50000,
  "transport": 100000,
  "workerCount": 2,
  "wagePerPerson": 100000
}
```

Server calculations returned on records and totals:

- `workerCost = workerCount * wagePerPerson`.
- Harvest `grossSales = weightKg * unitPrice`; `totalCost = deductions + transport + workerCost`; `netIncome = grossSales - totalCost`.
- Fertilizing `materialQuantity = treeCount * dosePerTreeKg` (kg); `materialCost = materialQuantity * unitPrice`.
- Spraying `materialQuantity = treatedAreaHa * doseLitersPerHa` (liters); `materialCost = materialQuantity * unitPrice`.
- Pupuk/Semprot `totalCost = materialCost + workerCost`.
- Pruning `totalCost = treeCount * wagePerTree`.

Semprot area cannot exceed the parcel's legal area; a missing legal area must be fixed in land master data first. Numeric inputs cannot be negative. Totals include finalized records only. Drafts/reopened records remain visible but are excluded from financial totals. Material quantity units differ by activity; never add fertilizer kg and pesticide liters together.

`status`: `Draft`, `Finalized`, `Reopened`. Only editable states permit PUT/DELETE. Reopening needs a reason. Historical farmer name/NIK/legality/area snapshots are server-owned and stay unchanged during edits.

## MICS submission lifecycle

Monitoring remains ongoing per Poktan, independent of certification cycles. Reuse the existing `POST /api/monitoring/{type-route}` to create the period report and `GET /api/monitoring/submissions?associationId=...&poktanId=...&type=...&year=...` to find it. Then add/edit observations through the new row APIs below.

`GET /api/monitoring/definitions` provides effective frequency/name/required count. Super Admin `PUT /api/monitoring/definitions/{type}` configures `{name,group,frequency,requiredOccurrencesPerYear,isActive}`. `frequency` is `Annual` or `SemiAnnual`. Use January 1–December 31 for annual periods; January 1–June 30 or July 1–December 31 for semiannual periods. Defaults: P3K and conservation semiannual, all others annual. Changing frequency affects validation of new report writes; historical periods remain stored.

Create an empty draft using the common header and its empty collection:

```json
{
  "poktanId": "<poktan-id>",
  "periodStart": "2026-01-01",
  "periodEnd": "2026-12-31",
  "summary": "",
  "inspections": []
}
```

For MICS 8/9 use `incidents: []`, for MICS 13 `complaints: []`, for MICS 7 `locations: [], speciesObservations: []`. Empty drafts are permitted; finalization requires observations or an explicit Nihil declaration for event types.

| Action | Endpoint |
|---|---|
| Get full report | `GET /api/monitoring/submissions/{id}` |
| Finalize | `POST /api/monitoring/submissions/{id}/finalize` |
| Reopen | `POST /api/monitoring/submissions/{id}/reopen` with `{reason}` |
| Soft delete draft/reopened report | `DELETE /api/monitoring/submissions/{id}` |
| Add observation | `POST /api/monitoring/submissions/{id}/observations` |
| Replace one observation | `PUT /api/monitoring/submissions/{id}/observations/{observationId}` |
| Declare no events | `POST /api/monitoring/submissions/{id}/zero-declaration` with `{"declaration":"Tidak ada insiden"}` |
| Soft delete one observation | `DELETE /api/monitoring/submissions/{id}/observations/{observationId}` |

Finalized reports are immutable until reopened. Adding an event clears an existing Nihil flag. Removing the last event does not automatically declare Nihil. Use `POST /submissions/{id}/zero-declaration` for Nihil; it rejects a report that still contains events. The legacy type-specific PUT also accepts an empty event collection and `noIncidents: true, zeroIncidentDeclaration: "Tidak ada ..."`, or `noComplaints: true, zeroComplaintDeclaration: "Tidak ada pengaduan"`. Include the common period header. Do not use Nihil to represent a missing report.

Observation IDs identify groups of rows. Multi-type observations share one parcel/date; each row has a server-generated ID. Replacing an observation keeps its observation ID but generates new row IDs; do not retain row IDs as edit targets. PATCH is not supported. Whole-report PUT remains a legacy replacement interface; use row APIs for normal edits to preserve unrelated observations and their snapshots.

### MICS table wiring

`GET /api/monitoring/table?associationId=...&type=Weed&year=2026` with optional `poktanId`, `petaniId`, `lahanId`, report `status`, search/pagination.

Returns paginated parcel rows, including empty ones, with `land`, `observationCount`, `latestObservedOn`, `monitoringStatus`, `observations: [{observationId,submissionId,status,rows}]`. `land.areaHa` and `land.areaM2` use legal area; `noLegalitas` remains the MICS label. The observation list supplies expansion/history. Use latest observed date for the collapsed row.

Date status: `Current` (finalized observation in required period), `Pending` (deadline not passed and no confirmed current observation), `Overdue` (deadline passed). Green = Current, red = Overdue, neutral = Pending. Item severity/condition uses separate badges. Historical dates may be green if their required period is satisfied; this is not a generic age-based rule.

Use specialized endpoints for non-parcel data:

- `GET /api/monitoring/ppe-table?associationId=...&year=2026`: one row per farmer with grouped PPE activity observations; optional `poktanId`, search/pagination.
- `GET /api/monitoring/first-aid-table?associationId=...&year=2026&locationId=...`: location rows with inspection history and items. `locationId` optional.
- `GET /api/monitoring/complaints-table?associationId=...&year=2026`: farmer rows with all identified complaints, including complaints with no parcel. Optional `poktanId`, search/pagination.
- `GET /api/monitoring/complaints?associationId=...&year=2026`: flat complaints, including authorized anonymous complaints; optional `poktanId`, `petaniId`, complaint `status`, search/pagination.

Complaint report GET/export/attachments are restricted to Association/Super Admin to prevent confidential metadata leakage. Other authorized users read filtered complaints through the complaint endpoints. Do not show confidential badges/details or counts from an unfiltered source.

### Row payloads

All observation requests use `{"rows":[{...}]}`. MICS 7 may add `"rowKind":"location"`; omit it or use `"species"` for species observations.

Common parcel row fields: `petaniId`, `lahanId`, `observedOn`, optional `notes`, optional `followUp`. Parcel is required except PPE (farmer-level allowed). Send no snapshots, IDs, navigation properties, or deletion flags.

| MICS | Enum `type` / create route | Row fields beyond common identity/date |
|---|---|---|
| 1 | `LandBoundaryMarker` / `land-boundaries` | `installationYear` (year only), `markerCount`, `condition`: Good/Damaged/Missing |
| 2 | `Turnera` / `turnera` | `condition`: Good/Dead; optional `description` |
| 3 | `ChemicalBufferBoundary` / `chemical-buffers` | `hasRiverBoundaryMarker`, `noChemicalActivityWithinFiveMeters`, `hasWoodyPlantsWithinFiveMeters`, `noPlantingOnSteepSlope` |
| 4 | `WoodyPlantAndErosionControl` / `woody-plants` | `observations: [{treeName,quantity,heightCentimeters,damageSymptoms,remarks}]` |
| 5 | `FirstAidKit` / `first-aid-kits` | One row: `locationId`, `observedOn`, `notes`, `items: [{itemName,condition,notes,followUp}]`; no farmer/parcel fields |
| 6 | `PersonalProtectiveEquipment` / `ppe` | One row per activity: `activity`: Harvesting/Spraying/Pruning/Fertilizing/Other, `items: [{itemName,isUsed,notes}]`; checkbox means used; availability is not inferred |
| 7 | `HighConservationValue` / `high-conservation-values` | Multiple species rows: `speciesKind`: Animal/Plant, `speciesName`, optional `description`; location rows use `semester` and `location` |
| 8 | `FireIncident` / `fires` | `incidentDate`, `severity`: Light/Moderate/Severe, `chronology`, notes/follow-up |
| 9 | `WorkplaceAccident` / `workplace-accidents` | `incidentDate`, `category`: Insignificant/Minor/Moderate/Major/Disaster, `caseCount` (people affected), `chronology` |
| 10 | `Weed` / `weeds` | `weedType`, `result`, `treatment`, notes; multiple type rows allowed |
| 11 | `PlantDisease` / `plant-diseases` | `diseaseType`, `result`, `treatment`, notes; multiple type rows allowed |
| 12 | `Pest` / `pests` | `pestType`, `observedDensity`, `densityUnit`, `severity`: None/Light/Moderate/Severe, `treatment`, notes; multiple type rows allowed |
| 13 | `MemberComplaint` / `member-complaints` | One row: `petaniId` (unless anonymous), optional `lahanId`, `receivedOn`, `complaintType`, `description`, `notes`, `followUp`, `status`, `resolvedOn`, `closureReason`, `isAnonymous`, `isConfidential` |

MICS 8/9 `observedOn` is monitoring/reporting date, distinct from `incidentDate`; incident date cannot be after reporting date. Count **incidents** using observation records; sum `caseCount` separately for affected people. MICS 12 severity is manual; the mockup threshold chart is informational, not an automatic classification formula. Density unit is required when density is supplied.

Complaint statuses: `Open`, `InProgress`, `Resolved`, `Closed`. Resolved requires `resolvedOn`; Closed requires `closureReason` (e.g. resolved, withdrawn, rejected). `Rejected` is retained only as a legacy read value. Anonymous writes remove farmer name/NIK from the server record. Identified complaint parcel ownership is checked. The complaint date is `receivedOn`, not the incident date used in MICS 8/9.

Example multi-pest observation:

```json
{
  "rows": [
    {"petaniId":"<farmer-id>","lahanId":"<parcel-id>","observedOn":"2026-10-06","pestType":"Ulat","observedDensity":3,"densityUnit":"ulat/pokok","severity":"Light","treatment":"Pengendalian biologis"},
    {"petaniId":"<farmer-id>","lahanId":"<parcel-id>","observedOn":"2026-10-06","pestType":"Tikus","observedDensity":10,"densityUnit":"pokok/ha","severity":"Light"}
  ]
}
```

POST/PUT observation success: `{observationId,rowIds}`. Reload the table/report after saving.

## Bulk Input: manual rows and Excel

Both inputs use the same validation, permissions, duplicate decisions, and commit flow. All rows must pass before any are saved. Imports create drafts; they do not finalize reports/logs.

Field-log endpoints:

- `GET /api/field-logs/import/template`
- `POST /api/field-logs/import/excel-preview` multipart fields `associationId`, `file`
- `POST /api/field-logs/import/preview?associationId=...` JSON
- `POST /api/field-logs/import?associationId=...` JSON commit

MICS endpoints:

- `GET /api/monitoring/import/template?type=Pest` (optional `rowKind` for conservation)
- `POST /api/monitoring/submissions/{id}/import/excel-preview` multipart `file`, optional `rowKind`
- `POST /api/monitoring/submissions/{id}/import/preview?rowKind=...` JSON
- `POST /api/monitoring/submissions/{id}/import?rowKind=...` JSON commit

JSON request:

```json
{
  "rows": [
    {
      "nik": "1111111111111111",
      "landLegalNumber": "LAHAN-A",
      "duplicateChoice": null,
      "data": {"activity":"Fertilizing","activityDate":"2026-10-06","materialName":"Urea","treeCount":100,"dosePerTreeKg":2,"unitPrice":5000,"workerCount":2,"wagePerPerson":100000}
    }
  ]
}
```

Each field-log or parcel observation row requires NIK + legality number. The server resolves ownership and overwrites payload identity fields. Exception: P3K is location-based and uses `locationId` without farmer identity; a general identified complaint can use NIK without a parcel; anonymous complaints need neither identity field.

JSON preview returns `{isValid,rows:[{rowNumber,potentialDuplicate,duplicateChoice,errors}]}`. Excel preview returns `{preview:<same shape>,inputRows:[<normalized JSON import rows>]}`. Keep `inputRows`, let users correct rows/set choices, re-preview, then submit `{rows: inputRows}` to JSON commit. Preview never persists data; commit repeats validation against current database state.

Duplicates are deliberately flagged as **potential**, not rejected as identical transactions. Field logs match parcel/type/date/material/buyer; MICS matches subject/date/type. In-file repeats are also flagged. Set `duplicateChoice: "skip"` or `"append"` explicitly on flagged rows. No silent update/overwrite is supported.

XLSX only, max 10 MB compressed / 40 MB extracted, up to 1000 rows. Use supplied headers. NIK/legality numbers must be text to preserve leading zeros. Dates must be ISO text (not Excel serial dates). Scalar columns contain normal values; nested list columns such as `Items`/`Observations` contain JSON arrays. Enum columns use API enum strings. Empty columns use model defaults/null. Raw parse errors reject the preview; parsed business-rule errors appear against each row. Commit is atomic on PostgreSQL. On sequence/concurrency conflicts reload/re-preview and retry.

## Errors and refresh behavior

- `400`: invalid inputs, period/date/area constraints, unresolved duplicates, or editing a finalized ICS observation; inspect `detail` or row `errors`.
- `403`: scope/role denies action.
- `404`: missing or invisible record.
- `409`: finalized training/log locks, duplicate keys, or concurrent changes; reload before retrying.

Standard failures use ProblemDetails (`status`, `title`, `detail`, `traceId`), with model-validation `errors` where applicable. Bulk validation failures return the preview object instead. Successful mutations usually return 204; creates return IDs (field-log create also returns calculated data). Re-fetch affected tables, summary cards, and totals after mutations.
