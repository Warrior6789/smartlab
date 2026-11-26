-- Admin activates a roster account by hand with a temporary password; the student must change it at first login.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;
