-- Team formation per class: the lecturer sets the team size, opens self-selection, locks it, then places the rest.
-- Run once on the database, then re-run scripts/scaffold.ps1 (the DAL changes in this commit match what it generates).

BEGIN;

ALTER TABLE classes
    ADD COLUMN min_team_size            integer     NULL,
    ADD COLUMN max_team_size            integer     NULL,
    ADD COLUMN team_formation_status    varchar(20) NOT NULL DEFAULT 'NotStarted',
    ADD COLUMN team_formation_opened_at timestamptz NULL,
    ADD COLUMN team_formation_deadline  timestamptz NULL;

ALTER TABLE classes
    ADD CONSTRAINT ck_classes_team_size CHECK (
        (min_team_size IS NULL OR min_team_size >= 1) AND
        (max_team_size IS NULL OR max_team_size >= 1) AND
        (min_team_size IS NULL OR max_team_size IS NULL OR min_team_size <= max_team_size)),
    ADD CONSTRAINT ck_classes_team_formation_status
        CHECK (team_formation_status IN ('NotStarted', 'Open', 'Locked'));

-- Existing data: classes whose students already formed teams keep working as before (open, no size limit).
UPDATE classes c
SET team_formation_status = 'Open',
    team_formation_opened_at = now()
WHERE EXISTS (SELECT 1 FROM teams t WHERE t.class_id = c.class_id);

COMMIT;
