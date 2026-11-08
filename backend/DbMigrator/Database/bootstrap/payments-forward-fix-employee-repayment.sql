alter table finance_payments.payment drop constraint if exists payment_payment_type_check;
alter table finance_payments.payment add constraint payment_payment_type_check check (payment_type in ('CustomerReceipt','SupplierPayment','CustomerAdvance','SupplierAdvance','CustomerRefund','SupplierRefund','ExpensePayment','EmployeeRepayment','Standalone','TransferLeg','ProviderFee'));
alter table business_expenses.expense_employee_repayment add column if not exists settled_amount numeric(19,4) not null default 0;
alter table business_expenses.expense_employee_repayment add column if not exists payment_id uuid;
alter table business_expenses.expense_employee_repayment add column if not exists version bigint not null default 1;
alter table business_expenses.expense_employee_repayment add constraint expense_employee_repayment_settled_amount_check check (settled_amount >= 0 and settled_amount <= amount) not valid;
create index if not exists ix_expenses_employee_repayment_payment on business_expenses.expense_employee_repayment(tenant_id,payment_id);
