ALTER TABLE inventory_audits
    ADD COLUMN IF NOT EXISTS draft jsonb NULL,
    ADD COLUMN IF NOT EXISTS draft_saved_at timestamptz NULL;

COMMENT ON COLUMN inventory_audits.draft IS 'Counting saved halfway while InProgress: counted lines (component, quantity) and units ticked missing; cleared on submit';
COMMENT ON COLUMN inventory_audits.draft_saved_at IS 'When the draft was last saved';
