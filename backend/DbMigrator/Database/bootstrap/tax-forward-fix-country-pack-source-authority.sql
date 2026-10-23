ALTER TABLE foundation_tax.tax_pack_activation
    ADD COLUMN IF NOT EXISTS source_authority text;

ALTER TABLE foundation_tax.tax_pack_activation NO FORCE ROW LEVEL SECURITY;

UPDATE foundation_tax.tax_pack_activation
SET source_authority = COALESCE(source_authority, authority_reference)
WHERE source_authority IS NULL;

ALTER TABLE foundation_tax.tax_pack_activation
    ALTER COLUMN source_authority SET NOT NULL;

ALTER TABLE foundation_tax.tax_pack_activation FORCE ROW LEVEL SECURITY;
