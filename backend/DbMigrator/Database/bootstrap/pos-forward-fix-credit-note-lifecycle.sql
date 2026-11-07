ALTER TABLE business_pos.pos_return
    ADD COLUMN IF NOT EXISTS credit_note_id uuid NULL,
    ADD COLUMN IF NOT EXISTS credit_note_state text NOT NULL DEFAULT 'NotRequested';
