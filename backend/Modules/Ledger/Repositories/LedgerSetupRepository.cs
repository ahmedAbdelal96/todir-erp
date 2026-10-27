using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Modules.Ledger.Services;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

public sealed class LedgerSetupRepository(
    NpgsqlDataSource dataSource,
    ITransactionCoordinator transactions,
    LedgerConfigurationCache cache,
    TimeProvider clock)
{
    private static readonly string[][] InvoiceRequiredRoles = [["AccountsReceivableControl", "Receivable"], ["AccountsPayableControl", "Payable"], ["SalesRevenue"], ["PurchaseExpense"], ["OutputTaxPayable", "OutputTax"], ["InputTaxRecoverable", "InputTax"]];
    private static readonly string[][] PaymentRequiredRoles = [["AccountsReceivableControl", "Receivable"], ["AccountsPayableControl"], ["Cash"], ["FXGain"], ["FXLoss"]];
    private static readonly string[][] InventoryRequiredRoles = [["InventoryAsset"], ["COGS"]];
    private static readonly string[][] PayrollRequiredRoles = [["PayrollExpense"], ["PayrollPayable"]];

    private static readonly (string Code, string Ar, string En, string Type, string Subtype, bool Postable, string? ParentCode)[] Accounts =
    [
        ("100000", "الأصول", "Assets", "Asset", "Other", false, null),
        ("110000", "النقدية والبنوك", "Cash and Banks", "Asset", "Cash", true, "100000"),
        ("120000", "العملاء", "Accounts Receivable", "Asset", "ReceivableControl", true, "100000"),
        ("130000", "المخزون", "Inventory", "Asset", "InventoryAsset", true, "100000"),
        ("200000", "الالتزامات", "Liabilities", "Liability", "Other", false, null),
        ("210000", "الموردون", "Accounts Payable", "Liability", "PayableControl", true, "200000"),
        ("220000", "ضريبة المخرجات", "Output Tax Payable", "Liability", "Tax", true, "200000"),
        ("221000", "ضريبة المدخلات القابلة للاسترداد", "Recoverable Input Tax", "Asset", "Tax", true, "100000"),
        ("230000", "التزامات الرواتب", "Payroll Payable", "Liability", "Payroll", true, "200000"),
        ("300000", "حقوق الملكية", "Equity", "Equity", "Other", false, null),
        ("310000", "الأرباح المحتجزة", "Retained Earnings", "Equity", "RetainedEarnings", true, "300000"),
        ("400000", "الإيرادات", "Revenue", "Revenue", "Revenue", false, null),
        ("410000", "إيرادات المبيعات", "Sales Revenue", "Revenue", "Revenue", true, "400000"),
        ("500000", "المصروفات", "Expenses", "Expense", "Other", false, null),
        ("510000", "تكلفة المبيعات", "Cost of Goods Sold", "Expense", "COGS", true, "500000"),
        ("520000", "مصروف الرواتب", "Payroll Expense", "Expense", "Payroll", true, "500000"),
        ("530000", "ديون معدومة", "Bad Debt Expense", "Expense", "BadDebt", true, "500000"),
        ("540000", "مصروفات المشتريات", "Purchase Expenses", "Expense", "Purchases", true, "500000"),
        ("420000", "إيرادات تسوية الديون", "Write-Off Income", "Revenue", "WriteOff", true, "400000"),
        ("999000", "أرباح فروق العملة", "FX Gain", "Revenue", "FXGain", true, null),
        ("999100", "خسائر فروق العملة", "FX Loss", "Expense", "FXLoss", true, null),
        ("999900", "فروق التقريب", "Rounding Difference", "Expense", "Rounding", true, null)
    ];

    private static readonly (string Code, string Ar, string En, string AccountCode)[] Roles =
    [
        ("AccountsReceivableControl", "حساب العملاء", "Accounts Receivable Control", "120000"),
        ("AccountsPayableControl", "حساب الموردين", "Accounts Payable Control", "210000"),
        ("SalesRevenue", "إيرادات المبيعات", "Sales Revenue", "410000"),
        ("PurchaseExpense", "مصروفات المشتريات", "Purchase Expenses", "540000"),
        ("Receivable", "حساب العملاء", "Receivables", "120000"),
        ("Payable", "حساب الموردين", "Payables", "210000"),
        ("OutputTax", "ضريبة المخرجات", "Output Tax", "220000"),
        ("InputTax", "ضريبة المدخلات", "Input Tax", "221000"),
        ("OutputTaxPayable", "ضريبة المخرجات", "Output Tax Payable", "220000"),
        ("InputTaxRecoverable", "ضريبة المدخلات القابلة للاسترداد", "Recoverable Input Tax", "221000"),
        ("Cash", "النقدية", "Cash", "110000"),
        ("InventoryAsset", "مخزون", "Inventory Asset", "130000"),
        ("COGS", "تكلفة المبيعات", "Cost of Goods Sold", "510000"),
        ("PayrollExpense", "مصروف الرواتب", "Payroll Expense", "520000"),
        ("PayrollPayable", "التزام الرواتب", "Payroll Payable", "230000"),
        ("BadDebtExpense", "مصروف الديون المعدومة", "Bad Debt Expense", "530000"),
        ("WriteOffIncome", "إيراد شطب الالتزام", "Write-Off Income", "420000"),
        ("RetainedEarnings", "الأرباح المحتجزة", "Retained Earnings", "310000"),
        ("FXGain", "أرباح فروق العملة", "FX Gain", "999000"),
        ("FXLoss", "خسائر فروق العملة", "FX Loss", "999100"),
        ("RoundingDifference", "فروق التقريب", "Rounding Difference", "999900")
    ];

    public async Task<ManagedLedgerSetupDto> InitializeAsync(TenantId tenant, LegalEntityId entity, ManagedLedgerSetupRequest request, Guid actor, Guid correlationId, CancellationToken ct)
    {
        var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var baseCurrency = await ScalarStringAsync(context, "select base_currency_code from platform.legal_entity where tenant_id=$1 and id=$2 and status='Active'", token, tenant.Value, entity.Value)
                ?? throw new LedgerDomainException("ledger.entity_missing", "The legal entity has no active financial context.");
            if (string.IsNullOrWhiteSpace(request.TemplateCode)) throw new LedgerDomainException("ledger.setup_template_missing", "A managed ledger setup template is required.");

            var existing = await LoadConfigurationAsync(context, tenant, entity, token);
            var configurationId = existing?.ConfigurationVersionId ?? await EnsureConfigurationAsync(context, tenant, entity, request.TemplateCode, token);
            var mode = request.Mode;
            var accountCount = 0;
            var journalCount = 0;
            var roleCount = 0;
            var mappingCount = 0;
            var periodCount = 0;

            var accounts = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var account in Accounts)
            {
                var id = await EnsureAccountAsync(context, tenant, entity, account, token);
                accounts[account.Code] = id;
                accountCount++;
            }
            foreach (var journal in new[] { ("SALES", "المبيعات", "Sales", "Sales"), ("PURCHASE", "المشتريات", "Purchases", "Purchases"), ("GENERAL", "عام", "General", "General"), ("OPENING", "افتتاحي", "Opening", "Opening"), ("FX", "فروق العملة", "Foreign exchange", "Fx"), ("ADJUSTMENT", "تسويات", "Adjustments", "General") })
            {
                await EnsureJournalAsync(context, tenant, entity, journal, token);
                journalCount++;
            }
            var yearStart = request.FiscalYearStart ?? new DateOnly(DateTime.UtcNow.Year, 1, 1);
            var yearEnd = request.FiscalYearEnd ?? new DateOnly(yearStart.Year, 12, 31);
            var fiscalYearId = await EnsureFiscalYearAsync(context, tenant, entity, yearStart, yearEnd, token);
            for (var month = 1; month <= 12; month++)
            {
                var start = new DateOnly(yearStart.Year, month, 1);
                if (start < yearStart || start > yearEnd) continue;
                var end = month == 12 ? new DateOnly(yearStart.Year, 12, 31) : new DateOnly(yearStart.Year, month + 1, 1).AddDays(-1);
                if (end > yearEnd) end = yearEnd;
                await EnsurePeriodAsync(context, tenant, entity, fiscalYearId, month, start, end, token);
                periodCount++;
            }
            foreach (var role in Roles)
            {
                var roleId = await EnsureRoleAsync(context, tenant, entity, role, token);
                var mappingId = await EnsureMappingAsync(context, tenant, entity, role, roleId, token);
                var targetAccountId = accounts[role.AccountCode];
                if (role.Code == "Cash" && !await IsPostableAccountAsync(context, tenant, entity, targetAccountId, token))
                    targetAccountId = await FindPostableCashAccountAsync(context, tenant, entity, token)
                        ?? throw new LedgerDomainException("ledger.cash_account_missing", "Configure an active postable cash account before initializing Ledger.");
                await EnsureMappingVersionAsync(context, tenant, entity, configurationId, mappingId, targetAccountId, token);
                roleCount++;
                mappingCount++;
            }
            foreach (var producer in Producers)
                await EnsureProducerRegistrationAsync(context, tenant, entity, producer, token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.SettlementFxDifference, "SETTLEMENT_FX_DIFFERENCE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.SettlementWriteOff, "SETTLEMENT_WRITE_OFF_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.OpeningBalancePosted, "OPENING_BALANCE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.CustomerInvoicePosted, "CUSTOMER_INVOICE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.CustomerCreditNotePosted, "CUSTOMER_CREDIT_NOTE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.VendorBillPosted, "VENDOR_BILL_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.VendorCreditNotePosted, "VENDOR_CREDIT_NOTE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.CustomerDebitNotePosted, "CUSTOMER_DEBIT_NOTE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.PaymentReceived, "PAYMENT_RECEIVED_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.PaymentMade, "PAYMENT_MADE_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.RefundPaid, "PAYMENT_REFUND_PAID_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.PaymentFeeRecognized, "PAYMENT_FEE_RECOGNIZED_V1", token);
            await EnsureSimplePostingRuleAsync(context, tenant, entity, AccountingFactTypes.VendorDebitNotePosted, "VENDOR_DEBIT_NOTE_V1", token);

            var configWasCreated = existing is null;
            await ExecuteAsync(context, "insert into finance_ledger.ledger_entity_configuration (tenant_id,legal_entity_id,id,mode,setup_template_code,active_configuration_version_id,initialized_at,version) values ($1,$2,$3,$4,$5,$6,$7,1) on conflict (tenant_id,legal_entity_id) do update set mode=excluded.mode,setup_template_code=excluded.setup_template_code,active_configuration_version_id=excluded.active_configuration_version_id,version=finance_ledger.ledger_entity_configuration.version+1", token, tenant.Value, entity.Value, existing?.Id ?? Guid.CreateVersion7(), mode.ToString(), request.TemplateCode.Trim(), configurationId, clock.GetUtcNow());
            if (configWasCreated || existing!.Value.Mode != mode)
            {
                var payload = JsonSerializer.Serialize(new AccountingConfigurationActivatedEvent(configurationId, entity.Value, mode == LedgerMode.Managed ? LedgerModeValue.Managed : LedgerModeValue.Full, request.TemplateCode.Trim()));
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.AccountingConfigurationActivatedV1, payload, tenant, clock.GetUtcNow(), correlationId, configurationId, "ledger"));
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.LedgerConfigurationChanged, payload, tenant, clock.GetUtcNow(), correlationId, configurationId, "ledger"));
                await AuditAsync(context, tenant, entity, actor, existing is null ? "ledger.managed_setup.initialized" : "ledger.mode.changed", configurationId, existing is null ? null : new { mode = existing.Value.Mode.ToString() }, new { mode = mode.ToString(), template = request.TemplateCode }, correlationId, token);
            }
            return (configurationId, accountCount, journalCount, roleCount, mappingCount, periodCount);
        }, ct);

        cache.Invalidate(tenant, entity);
        var readiness = await GetReadinessAsync(tenant, entity, ct);
        return new ManagedLedgerSetupDto(entity.Value, request.Mode, true, result.configurationId, result.accountCount, result.journalCount, result.roleCount, result.mappingCount, result.periodCount, readiness);
    }

    public async Task<LedgerModeDto> GetModeAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select mode,version from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(entity.Value, Enum.Parse<LedgerMode>(reader.GetString(0)), reader.GetInt64(1)) : new(entity.Value, LedgerMode.Managed, 0);
    }

    public async Task<LedgerModeDto> ChangeModeAsync(TenantId tenant, LegalEntityId entity, LedgerMode mode, Guid actor, Guid correlationId, CancellationToken ct)
    {
        var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var existing = await LoadConfigurationAsync(context, tenant, entity, token) ?? throw new LedgerDomainException("ledger.setup_required", "Initialize the managed ledger before changing its mode.");
            if (existing.Mode == mode) return new LedgerModeDto(entity.Value, mode, existing.Version);
            await ExecuteAsync(context, "update finance_ledger.ledger_entity_configuration set mode=$4,version=version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity.Value, existing.Id, mode.ToString());
            var payload = JsonSerializer.Serialize(new { legalEntityId = entity.Value, mode = mode.ToString(), configurationVersionId = existing.ConfigurationVersionId });
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), LedgerEventTypes.LedgerConfigurationChanged, payload, tenant, clock.GetUtcNow(), correlationId, existing.ConfigurationVersionId, "ledger"));
            await AuditAsync(context, tenant, entity, actor, "ledger.mode.changed", existing.Id, new { mode = existing.Mode.ToString() }, new { mode = mode.ToString() }, correlationId, token);
            return new LedgerModeDto(entity.Value, mode, existing.Version + 1);
        }, ct);
        cache.Invalidate(tenant, entity);
        return result;
    }

    public async Task<LedgerReadinessDto> GetReadinessAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        var mode = LedgerMode.Managed;
        var version = 0L;
        Guid configurationId = Guid.Empty;
        await using (var configuration = new NpgsqlCommand("select mode,version,active_configuration_version_id from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2", connection))
        {
            configuration.Parameters.AddWithValue(tenant.Value); configuration.Parameters.AddWithValue(entity.Value);
            await using var reader = await configuration.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { mode = Enum.Parse<LedgerMode>(reader.GetString(0)); version = reader.GetInt64(1); configurationId = reader.GetGuid(2); }
        }
        var baseCurrency = await ScalarStringAsync(connection, "select base_currency_code from platform.legal_entity where tenant_id=$1 and id=$2 and status='Active'", ct, tenant.Value, entity.Value);
        var journal = await ScalarLongAsync(connection, "select count(*) from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='GENERAL' and status='Active'", ct, tenant.Value, entity.Value);
        var period = await ScalarLongAsync(connection, "select count(*) from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and status='Open'", ct, tenant.Value, entity.Value);
        var requiredRoles = await LoadMappedRolesAsync(connection, tenant, entity, ct);
        var capabilities = new List<LedgerCapabilityReadiness>
        {
            Capability("CanPostInvoices", baseCurrency is null || journal == 0 || period == 0 || !HasAllRoles(requiredRoles, InvoiceRequiredRoles), baseCurrency is null ? new("ledger.base_currency_missing", "Base currency is not configured.") : journal == 0 ? new("ledger.journal_missing", "The General journal is not configured.") : period == 0 ? new("ledger.period_missing", "No open accounting period exists.") : new("ledger.account_mapping_missing", "Required invoice account mappings are incomplete.")),
            Capability("CanPostPayments", baseCurrency is null || journal == 0 || period == 0 || !HasAllRoles(requiredRoles, PaymentRequiredRoles), baseCurrency is null ? new("ledger.base_currency_missing", "Base currency is not configured.") : new("ledger.payment_configuration_incomplete", "Payment control and FX account configuration is incomplete.")),
            Capability("CanPostInventory", baseCurrency is null || journal == 0 || period == 0 || !HasAllRoles(requiredRoles, InventoryRequiredRoles), baseCurrency is null ? new("ledger.base_currency_missing", "Base currency is not configured.") : new("ledger.inventory_configuration_incomplete", "Inventory account configuration is incomplete.")),
            Capability("CanPostPayroll", baseCurrency is null || journal == 0 || period == 0 || !HasAllRoles(requiredRoles, PayrollRequiredRoles), baseCurrency is null ? new("ledger.base_currency_missing", "Base currency is not configured.") : new("ledger.payroll_configuration_incomplete", "Payroll account configuration is incomplete.")),
            Capability("CanPostGeneralJournal", journal == 0 || period == 0, journal == 0 ? new("ledger.journal_missing", "The General journal is not configured.") : new("ledger.period_missing", "No open accounting period exists.")),
            Capability("CanClosePeriod", baseCurrency is null || period == 0 || configurationId == Guid.Empty, baseCurrency is null ? new("ledger.base_currency_missing", "Base currency is not configured.") : new("ledger.period_missing", "No configured accounting period is available."))
        };
        var state = capabilities.Any(x => x.State == LedgerReadinessState.Blocked) ? LedgerReadinessState.Blocked : capabilities.Any(x => x.State == LedgerReadinessState.Warning) ? LedgerReadinessState.Warning : LedgerReadinessState.Ready;
        var snapshot = new LedgerConfigurationSnapshot(mode, configurationId, version, clock.GetUtcNow());
        cache.Set(tenant, entity, snapshot);
        return new LedgerReadinessDto(entity.Value, mode, state, capabilities, version);
    }

    private static LedgerCapabilityReadiness Capability(string name, bool blocked, LedgerReadinessReason reason) => new(name, blocked ? LedgerReadinessState.Blocked : LedgerReadinessState.Ready, blocked ? [reason with { Capability = name }] : []);
    private static bool HasAllRoles(ISet<string> available, IReadOnlyList<string[]> required) => required.All(group => group.Any(available.Contains));

    private async Task<Guid> EnsureAccountAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, (string Code, string Ar, string En, string Type, string Subtype, bool Postable, string? ParentCode) account, CancellationToken ct)
    {
        var existing = await ScalarGuidAsync(context, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code=$3", ct, tenant.Value, entity.Value, account.Code);
        if (existing is not null) return existing.Value;
        var id = Guid.CreateVersion7();
        var normal = account.Type is "Asset" or "Expense" ? "Debit" : "Credit";
        var parentId = account.ParentCode is null ? null : await ScalarGuidAsync(context, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and code=$3", ct, tenant.Value, entity.Value, account.ParentCode);
        await ExecuteAsync(context, "insert into finance_ledger.account (tenant_id,legal_entity_id,id,code,name_ar,name_en,account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Active',1,$12,$12)", ct, tenant.Value, entity.Value, id, account.Code, account.Ar, account.En, account.Type, account.Subtype, normal, (object?)parentId ?? DBNull.Value, account.Postable, clock.GetUtcNow());
        return id;
    }

    private static async Task EnsureJournalAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, (string Code, string Ar, string En, string Type) journal, CancellationToken ct) => await ExecuteAsync(context, "insert into finance_ledger.journal (tenant_id,legal_entity_id,id,code,name_ar,name_en,journal_type,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Active',1,$8,$8) on conflict (tenant_id,legal_entity_id,code) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), journal.Code, journal.Ar, journal.En, journal.Type, DateTimeOffset.UtcNow);

    private static async Task<Guid> EnsureFiscalYearAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var existing = await ScalarGuidAsync(context, "select id from finance_ledger.fiscal_year where tenant_id=$1 and legal_entity_id=$2 and start_date=$3 and end_date=$4", ct, tenant.Value, entity.Value, start, end);
        if (existing is not null) return existing.Value;
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.fiscal_year (tenant_id,legal_entity_id,id,code,start_date,end_date,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,'Open',1,now(),now())", ct, tenant.Value, entity.Value, id, $"FY-{start.Year}", start, end); return id;
    }

    private static async Task EnsurePeriodAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid year, int month, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var existing = await ScalarGuidAsync(context, "select id from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and fiscal_year_id=$3 and start_date=$4 and end_date=$5", ct, tenant.Value, entity.Value, year, start, end);
        if (existing is not null) return;
        await ExecuteAsync(context, "insert into finance_ledger.accounting_period (tenant_id,legal_entity_id,id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,'Open',1,now(),now()) on conflict (tenant_id,fiscal_year_id,code) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), year, $"P{month:00}", start, end, month);
    }

    private static async Task<Guid> EnsureRoleAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, (string Code, string Ar, string En, string AccountCode) role, CancellationToken ct)
    {
        var existing = await ScalarGuidAsync(context, "select id from finance_ledger.account_role where tenant_id=$1 and legal_entity_id=$2 and code=$3", ct, tenant.Value, entity.Value, role.Code);
        if (existing is not null) return existing.Value;
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.account_role (tenant_id,legal_entity_id,id,code,name_ar,name_en,status,version) values ($1,$2,$3,$4,$5,$6,'Active',1)", ct, tenant.Value, entity.Value, id, role.Code, role.Ar, role.En); return id;
    }

    private static async Task<Guid> EnsureMappingAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, (string Code, string Ar, string En, string AccountCode) role, Guid roleId, CancellationToken ct)
    {
        var existing = await ScalarGuidAsync(context, "select id from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2 and role_id=$3 and status='Active' order by code limit 1", ct, tenant.Value, entity.Value, roleId);
        existing ??= await ScalarGuidAsync(context, "select id from finance_ledger.account_mapping where tenant_id=$1 and legal_entity_id=$2 and code=$3", ct, tenant.Value, entity.Value, $"MANAGED-{role.Code}");
        if (existing is not null) return existing.Value;
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.account_mapping (tenant_id,legal_entity_id,id,role_id,code,status,version) values ($1,$2,$3,$4,$5,'Active',1)", ct, tenant.Value, entity.Value, id, roleId, $"MANAGED-{role.Code}"); return id;
    }

    private static async Task<bool> IsPostableAccountAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid accountId, CancellationToken ct) =>
        await ScalarGuidAsync(context, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active' and allow_direct_posting=true", ct, tenant.Value, entity.Value, accountId) == accountId;

    private static async Task<Guid?> FindPostableCashAccountAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, CancellationToken ct) =>
        await ScalarGuidAsync(context, "select id from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and allow_direct_posting=true and subtype='Cash' order by code limit 1", ct, tenant.Value, entity.Value);

    private static async Task EnsureMappingVersionAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid configurationId, Guid mappingId, Guid accountId, CancellationToken ct) => await ExecuteAsync(context, "insert into finance_ledger.account_mapping_version (tenant_id,legal_entity_id,id,mapping_id,accounting_configuration_version_id,version_number,effective_from,specificity_rank,target_account_id,status) values ($1,$2,$3,$4,$5,1,current_date,0,$6,'Active') on conflict (tenant_id,mapping_id,version_number) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), mappingId, configurationId, accountId);

    private async Task EnsureSimplePostingRuleAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, string factType, string code, CancellationToken ct)
    {
        await ExecuteAsync(context, "insert into finance_ledger.posting_rule(tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at) values($1,$2,$3,$4,1,$5,'Active',1,$6) on conflict(tenant_id,legal_entity_id,fact_type,fact_version,code) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), factType, code, clock.GetUtcNow());
        var ruleId = await ScalarGuidAsync(context, "select id from finance_ledger.posting_rule where tenant_id=$1 and legal_entity_id=$2 and fact_type=$3 and fact_version=1 and code=$4", ct, tenant.Value, entity.Value, factType, code)
            ?? throw new LedgerDomainException("ledger.posting_rule_missing", "A managed posting rule could not be created.");
        await ExecuteAsync(context, "insert into finance_ledger.posting_rule_version(tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status) values($1,$2,$3,$4,1,current_date,'Active') on conflict(tenant_id,posting_rule_id,version_number) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), ruleId);
        var versionId = await ScalarGuidAsync(context, "select id from finance_ledger.posting_rule_version where tenant_id=$1 and legal_entity_id=$2 and posting_rule_id=$3 and version_number=1", ct, tenant.Value, entity.Value, ruleId)
            ?? throw new LedgerDomainException("ledger.posting_rule_missing", "A managed posting rule version could not be created.");
        var legs = factType switch
        {
            AccountingFactTypes.PaymentReceived => new[] { ("Cash", "BaseAmount", "Debit"), ("Receivable", "BaseAmount", "Credit") },
            AccountingFactTypes.PaymentMade => new[] { ("Payable", "BaseAmount", "Debit"), ("Cash", "BaseAmount", "Credit") },
            AccountingFactTypes.RefundPaid => new[] { ("Receivable", "BaseAmount", "Debit"), ("Cash", "BaseAmount", "Credit") },
            AccountingFactTypes.PaymentFeeRecognized => new[] { ("PaymentFeeExpense", "BaseAmount", "Debit"), ("Cash", "BaseAmount", "Credit") },
            AccountingFactTypes.SettlementWriteOff => new[] { ("Receivable", "BaseAmount", "Credit"), ("BadDebtExpense", "BaseAmount", "Debit"), ("Payable", "BaseAmount", "Debit"), ("WriteOffIncome", "BaseAmount", "Credit") },
            AccountingFactTypes.SettlementFxDifference => new[] { ("Receivable", "BaseAmount", "Debit"), ("FXGain", "BaseAmount", "Credit"), ("FXLoss", "BaseAmount", "Debit") },
            _ => new[] { ("OpeningBalance", "BaseAmount", "Debit"), ("OpeningBalance", "BaseAmount", "Credit") }
        };
        for (var i = 0; i < legs.Length; i++)
        {
            var leg = legs[i];
            await ExecuteAsync(context, "insert into finance_ledger.posting_rule_leg(tenant_id,legal_entity_id,id,posting_rule_version_id,sequence_no,semantic_role,amount_expression,direction) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict(tenant_id,posting_rule_version_id,sequence_no) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), versionId, i + 1, leg.Item1, leg.Item2, leg.Item3);
        }
    }

    private static readonly (string Module, string FactType)[] Producers =
    [
        ("invoicing", AccountingFactTypes.CustomerInvoicePosted), ("invoicing", AccountingFactTypes.CustomerCreditNotePosted),
        ("invoicing", AccountingFactTypes.VendorBillPosted), ("invoicing", AccountingFactTypes.VendorCreditNotePosted),
        ("invoicing", AccountingFactTypes.CustomerDebitNotePosted), ("invoicing", AccountingFactTypes.VendorDebitNotePosted),
        ("sales", AccountingFactTypes.CustomerInvoicePosted), ("sales", AccountingFactTypes.CustomerCreditNotePosted),
        ("payments", AccountingFactTypes.PaymentReceived), ("payments", AccountingFactTypes.PaymentMade), ("payments", AccountingFactTypes.RefundPaid), ("payments", AccountingFactTypes.PaymentFeeRecognized),
        ("inventory", AccountingFactTypes.InventoryReceiptValued), ("inventory", AccountingFactTypes.InventoryIssueValued), ("inventory", AccountingFactTypes.InventoryCostAdjustment),
        ("payroll", AccountingFactTypes.PayrollPosted), ("tax", AccountingFactTypes.TaxAdjustment),
        ("subledger", AccountingFactTypes.SettlementFxDifference), ("subledger", AccountingFactTypes.SettlementWriteOff)
    ];

    private async Task EnsureProducerRegistrationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, (string Module, string FactType) producer, CancellationToken ct) => await ExecuteAsync(context, "insert into finance_ledger.fact_producer_registration (tenant_id,legal_entity_id,id,producer_module,fact_type,fact_version,status,registered_at) values ($1,$2,$3,$4,$5,1,'Active',$6) on conflict (tenant_id,legal_entity_id,producer_module,fact_type,fact_version) do nothing", ct, tenant.Value, entity.Value, Guid.CreateVersion7(), producer.Module, producer.FactType, clock.GetUtcNow());

    private static async Task<HashSet<string>> LoadMappedRolesAsync(NpgsqlConnection connection, TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select r.code from finance_ledger.account_role r join finance_ledger.account_mapping m on m.tenant_id=r.tenant_id and m.legal_entity_id=r.legal_entity_id and m.role_id=r.id join finance_ledger.account_mapping_version v on v.tenant_id=m.tenant_id and v.legal_entity_id=m.legal_entity_id and v.mapping_id=m.id and v.status='Active' where r.tenant_id=$1 and r.legal_entity_id=$2 and r.status='Active' and m.status='Active'", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<Guid> EnsureConfigurationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, string template, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.accounting_configuration_version (tenant_id,legal_entity_id,id,version_number,effective_from,status,snapshot_hash,created_at) values ($1,$2,$3,coalesce((select max(version_number)+1 from finance_ledger.accounting_configuration_version where tenant_id=$1 and legal_entity_id=$2),1),current_date,'Active',$4,now())", ct, tenant.Value, entity.Value, id, $"managed:{template}"); return id;
    }

    private static async Task<(Guid Id, LedgerMode Mode, Guid ConfigurationVersionId, long Version)?> LoadConfigurationAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,mode,active_configuration_version_id,version from finance_ledger.ledger_entity_configuration where tenant_id=$1 and legal_entity_id=$2 for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), Enum.Parse<LedgerMode>(reader.GetString(1)), reader.GetGuid(2), reader.GetInt64(3)) : null;
    }

    private static async Task AuditAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid actor, string action, Guid id, object? before, object? after, Guid correlation, CancellationToken ct) => await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, null, "ledger", action, "ledger_entity_configuration", id, before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after), null, "api", correlation, null, DateTimeOffset.UtcNow, null), ct);
    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct) { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private static async Task<string?> ScalarStringAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (string)value; }
    private static async Task<string?> ScalarStringAsync(NpgsqlConnection connection, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, connection); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (string)value; }
    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, connection); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? 0L : long.Parse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<Guid?> ScalarGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : (Guid)value; }
    private static void Add(NpgsqlCommand command, params object[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); await command.ExecuteNonQueryAsync(ct); }
}
