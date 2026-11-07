ALTER TABLE business_pos.pos_sale_line
    ADD COLUMN IF NOT EXISTS lot_id uuid NULL,
    ADD COLUMN IF NOT EXISTS serial_id uuid NULL;

ALTER TABLE business_pos.pos_return_line
    ADD COLUMN IF NOT EXISTS lot_id uuid NULL;
