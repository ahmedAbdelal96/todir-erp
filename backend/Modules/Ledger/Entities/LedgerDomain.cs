namespace Modules.Ledger.Entities;

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }
public enum NormalBalance { Debit, Credit }
public enum AccountStatus { Active, Archived }
public enum JournalType { Sales, Purchases, Bank, Cash, Inventory, Payroll, General, Opening, Fx, Tax }
public enum PeriodStatus { Open, SoftClosed, Closed }
public enum FiscalYearStatus { Open, Closed }
public enum FinancialLockType { General, Tax, Advisor, Module }
public enum FactStatus { Received, Posted, Rejected }
public enum EntryStatus { Posted }

public static class AccountRoleCodes
{
    public const string AccountsReceivableControl = "Receivable";
    public const string AccountsPayableControl = "Payable";
    public const string SalesRevenue = "SalesRevenue";
    public const string PurchaseExpense = "PurchaseExpense";
    public const string OutputTaxPayable = "OutputTax";
    public const string InputRecoverableTax = "InputTax";
    public const string InventoryAsset = "InventoryAsset";
    public const string CostOfGoodsSold = "COGS";
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string PayrollExpense = "PayrollExpense";
    public const string PayrollPayable = "PayrollPayable";
    public const string FxGain = "FXGain";
    public const string FxLoss = "FXLoss";
    public const string RoundingDifference = "RoundingDifference";
}

public static class LedgerDomainRules
{
    public static NormalBalance NormalBalanceFor(AccountType type) => type is AccountType.Liability or AccountType.Equity or AccountType.Revenue ? NormalBalance.Credit : NormalBalance.Debit;
    public static bool IsValidAmount(decimal value) => value >= 0m;
    public static void EnsureBalanced(decimal debit, decimal credit)
    {
        if (debit <= 0m || credit <= 0m || debit != credit) throw new LedgerDomainException("ledger.entry_unbalanced", "Journal entry debit and credit totals must be equal and greater than zero.");
    }
}

public sealed class LedgerDomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
