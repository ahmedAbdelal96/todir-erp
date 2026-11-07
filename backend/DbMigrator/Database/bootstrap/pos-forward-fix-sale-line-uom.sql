ALTER TABLE business_pos.pos_sale_line
    ADD COLUMN IF NOT EXISTS unit_of_measure_id uuid NULL;
