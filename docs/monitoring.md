# ICS monitoring

Monitoring is a continuous Poktan/Sub-ICS activity and is independent of certification cycles. A submission is scoped to one Poktan, may contain any active farmer in that Poktan, and stores farmer/land identity snapshots so historical reports do not change with master data.

## Types and frequency

| MICS | API type | Default frequency |
|---|---|---|
| 1 Patok Batas Lahan | `LandBoundaryMarker` | Annual |
| 2 Bunga Turnera | `Turnera` | Annual |
| 3 Patok Batas Semprot/Pupuk Kimia | `ChemicalBufferBoundary` | Annual |
| 4 Tanaman Berkayu/Pengendalian Erosi | `WoodyPlantAndErosionControl` | Annual |
| 5 Isi Kotak P3K | `FirstAidKit` | Semiannual |
| 6 Alat Pelindung Diri | `PersonalProtectiveEquipment` | Annual |
| 7 Nilai Konservasi Tinggi | `HighConservationValue` | Semiannual |
| 8 Kebakaran | `FireIncident` | Annual |
| 9 Kecelakaan Kerja | `WorkplaceAccident` | Annual |
| 10 Gulma | `Weed` | Annual |
| 11 Penyakit Tanaman | `PlantDisease` | Annual |
| 12 Hama | `Pest` | Annual |
| 13 Pengaduan Anggota | `MemberComplaint` | Annual |

Annual periods are January 1 through December 31. Semiannual periods are January 1 through June 30 and July 1 through December 31. Definitions and required occurrence counts can be overridden by a Super Admin.

## Lifecycle and authorization

Submissions use `Draft`, `Finalized`, and `Reopened`. Drafts and reopened submissions are editable; finalized submissions are immutable. Reopening requires a reason. Association Admin and Poktan Admin may finalize, while only Association Admin or Super Admin may reopen. Assigned ICS auditors may create/edit drafts for their Poktan. Farmers may view submissions belonging to their Poktan but may not edit official reports.

Open follow-ups do not prevent finalization. Fire, accident, and complaint monitoring must contain rows or an explicit zero-event declaration. Attachments are optional and accept validated PDF, JPEG, or PNG files.

## API

- `GET /api/monitoring/definitions`
- `PUT /api/monitoring/definitions/{type}` (Super Admin)
- `GET|POST /api/monitoring/reference-items`
- `GET /api/monitoring/submissions`
- `GET|DELETE /api/monitoring/submissions/{id}`
- `POST /api/monitoring/submissions/{id}/finalize`
- `POST /api/monitoring/submissions/{id}/reopen`
- `POST|PUT /api/monitoring/submissions/{id}/follow-ups[/{followUpId}]`
- `POST|GET|DELETE /api/monitoring/submissions/{id}/attachments[/{attachmentId}]`
- `GET /api/monitoring/submissions/{id}/export`
- `GET /api/monitoring/compliance`

Each type has a dedicated `POST` and `PUT` route: `land-boundaries`, `turnera`, `chemical-buffers`, `woody-plants`, `first-aid-kits`, `ppe`, `high-conservation-values`, `fires`, `workplace-accidents`, `weeds`, `plant-diseases`, `pests`, and `member-complaints`.

The old `/api/certification/monitoring` routes and `MonitoringRecord` remain temporarily for compatibility. Existing rows are treated as legacy/unclassified records and are not automatically mapped to a MICS type.

## Database rollout

The repository contains an earlier initial migration. The new association training and field-log changes and monitoring extensions intentionally have **no new migration**; generate and review one manually before deploying these changes. Keep the legacy `MonitoringRecord` and cycle training tables until historical data has been reviewed.

See [frontend integration guide](operations-frontend-guide.md) and [manual migration notes](operations-migration-notes.md) for new routes, calculations, permissions, and the training backfill.
