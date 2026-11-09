alter table business_expenses.expense_card_transaction
    drop constraint if exists expense_card_transaction_state_check;

alter table business_expenses.expense_card_transaction
    add constraint expense_card_transaction_state_check
    check (state in ('Unmatched','Pending','Cleared','Matched','Personal','Allocated','Refunded','Credited','Reversed','Disputed','Ignored'));

grant select, insert, update, delete on business_expenses.expense_card_transaction to erp_app;
