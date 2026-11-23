alter table business_quality.quality_disposition
    add column if not exists unit_of_measure_id uuid,
    add column if not exists operational_date date,
    add column if not exists posting_date date;
