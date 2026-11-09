create or replace function business_expenses.prevent_published_configuration_mutation()
returns trigger
language plpgsql
as $$
begin
    if old.status = 'Published'
       and not (
           tg_op = 'UPDATE'
           and tg_table_name = 'expense_policy_version'
           and new.status = 'Retired'
           and (to_jsonb(old) - 'status') = (to_jsonb(new) - 'status')
       ) then
        raise exception 'expenses.published_configuration_immutable' using errcode = '23514';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end;
$$;
