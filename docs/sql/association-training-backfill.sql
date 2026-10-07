-- Run manually AFTER generating/applying the operations schema migration and its catalog seeds.
-- This is a data backfill, not an EF migration. No application startup runs this script.
-- Legacy tables remain intact. Dates are resolved in Asia/Makassar rather than DB session time.
BEGIN;
INSERT INTO esertifikasi."AssociationTraining"
  ("Id", "Version", "AssociationId", "LegacySessionId", "OriginalCertificationCycleId", "PoktanId", "Title", "LegacyDescription", "StartDate", "EndDate", "CompletedAt", "CreatedByUserId", "IsDeleted")
SELECT t."Id", t."Id", c."AssociationId", t."Id", t."CertificationCycleId", NULL, t."Title", t."Description",
       (t."ScheduledAt" AT TIME ZONE 'Asia/Makassar')::date,
       (t."ScheduledAt" AT TIME ZONE 'Asia/Makassar')::date,
       t."CompletedAt", t."CreatedByUserId", false
FROM esertifikasi."TrainingSession" t
JOIN esertifikasi."CertificationCycle" c ON c."Id" = t."CertificationCycleId"
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO esertifikasi."AssociationTrainingDay" ("TrainingId", "Date")
SELECT "Id", "StartDate" FROM esertifikasi."AssociationTraining" WHERE "LegacySessionId" IS NOT NULL
ON CONFLICT DO NOTHING;

INSERT INTO esertifikasi."AssociationTrainingParticipant" ("TrainingId", "PetaniId")
SELECT a."TrainingSessionId", a."PetaniId" FROM esertifikasi."TrainingAttendance" a
JOIN esertifikasi."AssociationTraining" t ON t."LegacySessionId" = a."TrainingSessionId"
ON CONFLICT DO NOTHING;

INSERT INTO esertifikasi."DailyTrainingAttendance" ("TrainingId", "PetaniId", "Date", "Present", "Notes")
SELECT a."TrainingSessionId", a."PetaniId", t."StartDate", a."Status" = 1,
       CASE WHEN a."Status" = 3 THEN concat('Legacy Excused. ', a."Notes") ELSE a."Notes" END
FROM esertifikasi."TrainingAttendance" a
JOIN esertifikasi."AssociationTraining" t ON t."LegacySessionId" = a."TrainingSessionId"
WHERE a."Status" <> 0 -- Invited remains UNMARKED, never silently marked absent.
ON CONFLICT DO NOTHING;
COMMIT;
