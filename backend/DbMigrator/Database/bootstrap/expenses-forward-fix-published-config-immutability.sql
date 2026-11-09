create or replace function business_expenses.prevent_published_configuration_mutation()
returns trigger
language plpgsql
as $$
begin
    if old.status = 'Published' then
        raise exception 'expenses.published_configuration_immutable' using errcode = '23514';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end;
$$;

do $$
declare
    table_name text;
begin
    foreach table_name in array array['expense_category_version','expense_policy_version','expense_rate_policy'] loop
        if to_regclass('business_expenses.' || table_name) is not null
           and not exists (
               select 1 from pg_trigger t
               join pg_class c on c.oid = t.tgrelid
               join pg_namespace n on n.oid = c.relnamespace
               where t.tgname = table_name || '_published_immutable'
                 and n.nspname = 'business_expenses'
                 and c.relname = table_name
           ) then
            execute format('create trigger %I before update or delete on business_expenses.%I for each row execute function business_expenses.prevent_published_configuration_mutation()', table_name || '_published_immutable', table_name);
        end if;
    end loop;
end;
$$;
