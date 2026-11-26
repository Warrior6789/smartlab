-- Transferring the team leader waits for the chosen member to accept it.
ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS pending_leader_id uuid NULL REFERENCES student_profiles(student_profile_id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS leader_transfer_requested_at timestamptz NULL;
