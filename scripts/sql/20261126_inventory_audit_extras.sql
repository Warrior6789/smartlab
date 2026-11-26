-- Units found in the cabinet that the count list did not have (other cabinet, lost earlier, still on loan, or never received).
-- The lab staff records them while counting; the inventory manager resolves each one before approving.
CREATE TABLE IF NOT EXISTS inventory_audit_extras (
    extra_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    audit_id     uuid NOT NULL REFERENCES inventory_audits(audit_id) ON DELETE CASCADE,
    item_code    varchar(50) NULL,
    component_id uuid NULL REFERENCES components(component_id),
    note         varchar(500) NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT inventory_audit_extras_code_or_component CHECK (item_code IS NOT NULL OR component_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_inventory_audit_extras_audit ON inventory_audit_extras(audit_id);
