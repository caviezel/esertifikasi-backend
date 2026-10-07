# Manual schema rollout: training and monitoring

No EF migration or model snapshot was generated or changed for this implementation. Generate the migration yourself against the current EF model, review its SQL, and apply it before deploying these API changes. Existing legacy certification/training tables are retained.

## New tables

`HarvestRotation`, `FieldLog`, `ActivityCounter`, `AssociationTraining`, `AssociationTrainingDay`, `AssociationTrainingParticipant`, `DailyTrainingAttendance`, `TrainingTopic`, `TrainingPackage`, `TrainingPackageTopic`, `AssociationTrainingTopic`, `FirstAidLocation` (schema `esertifikasi`). See `OperationsModelConfiguration.cs` for relationships, precision, keys, concurrency tokens, and seed data.

Important keys:

- `ActivityCounter`: `(LahanId, Activity, Year)` with concurrency token `Version`. Never reset/delete counters when deleting a field log.
- `AssociationTrainingDay`: `(TrainingId, Date)`.
- `AssociationTrainingParticipant`: `(TrainingId, PetaniId)`.
- `DailyTrainingAttendance`: `(TrainingId, PetaniId, Date)`; foreign keys require a selected day and registered participant.
- `AssociationTraining.LegacySessionId`: unique nullable; identifies historical copies.
- New field-log decimals use precision `(18,4)`.

Seed topics: ISPO, RSPO, SKI/ICS, APD, K3. Stable IDs are `b6000000-0000-0000-0000-000000000001` through `...000005`. Package `b6000000-0000-0000-0001-000000000001` includes the first three topics. EF `HasData` defines these seeds; ensure your manually written migration includes them if you do not generate it from the model.

## Existing monitoring tables

- `MonitoringSubmission`: `IsDeleted`, `DeletedAt`, `Version` (UUID concurrency token); change period uniqueness to the filtered unique index `WHERE "IsDeleted" = false`.
- Every `FarmerLandMonitoringRow` concrete table: `IsDeleted`, `ObservationId`.
- `LandBoundaryInspection`: nullable `InstallationYear`; `InstalledOn` retained only for legacy compatibility.
- `FirstAidKitInspection`: `IsDeleted`, `ObservationId`, nullable `LocationId`.
- `FireIncident` and `WorkplaceAccidentIncident`: required `IncidentDate`.
- `MemberComplaint`: `IsDeleted`, `ObservationId`, nullable `LahanId`, `LandLegalNumberSnapshot`, `Notes`, `ClosureReason`; add the parcel foreign key.
- `ComplaintStatus.Closed` has numeric value 4. Existing value 3 (`Rejected`) remains readable for history; new writes must use `Closed` plus a reason.

Backfill BEFORE enforcing required dates and before enabling the new APIs:

1. Default existing `IsDeleted` values to false and initialize `MonitoringSubmission.Version` to a UUID (using its existing ID is sufficient for the initial value). Field logs and association trainings also have server-managed UUID concurrency tokens.
2. Set every existing observation row's `ObservationId = Id`. This preserves one observation per historical row; the new API groups multiple rows under one observation ID.
3. Derive `InstallationYear` from `InstalledOn` when present; retain unknown years as null.
4. For incident dates, inspect historical data. If exact incident dates are unavailable, use `ObservedOn` as the initial incident date and document that historical inference. Never leave required dates at .NET's `0001-01-01` default.
5. Create association P3K locations from distinct historical location strings, then set inspection `LocationId`. Resolve duplicate spelling/casing manually. Each submission's association determines the location's owner.
6. Review confidential/anonymous complaint identities and existing rejected records. Do not automatically convert rejected complaints to closed without retaining an explanatory closure reason.

If writing DDL manually, also inspect query filters and required foreign keys in `MonitoringModelConfiguration.cs`. The model snapshot remains unchanged until you create your migration.

## Training backfill

After the new schema exists, run the separately supplied [`sql/association-training-backfill.sql`](sql/association-training-backfill.sql) manually. It is idempotent for historical copies and does not mutate the old sessions:

- Association comes from the old certification cycle.
- Each old scheduled timestamp becomes a one-day training in `Asia/Makassar`.
- Copies participants, completion timestamp, creator, original cycle reference, and full description.
- `Attended` becomes `present: true`; `Absent` and `Excused` become false; `Invited` stays unmarked.
- The original Excused distinction is retained in notes and the untouched legacy record.
- Historical topic text remains in `LegacyDescription`; manually assign structured topic IDs where the mapping is known. Do not guess ISPO/RSPO/package membership from free text.

The frontend must switch to association training endpoints. Old cycle training endpoints remain compatibility endpoints for legacy records, not aliases of the new API. Do not dual-write. Existing certification progress continues to represent legacy cycle training; ongoing association trainings do not affect cycle progress.

## Units and deployment

The new contract uses hectares for parcel areas and treatment area. Confirm the historical `Lahan.LuasLegalitas` data is already in hectares; do not blindly divide historical values. MICS tables expose `areaM2 = areaHa * 10000` for display. Existing `NoPetaLahan` is exposed as `noLahan`; where absent use `lahanId` for identity. No new LHN numbering is introduced.

Apply schema, review/backfill historical data, then deploy the API and switch the frontend. No runtime code automatically applies migrations or executes the backfill.
