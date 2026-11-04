using Core.Identity;
using Npgsql;
using NpgsqlTypes;

namespace DbMigrator.SeedData;

public static class IdentitySeed
{
    private static readonly string[] Permissions =
    [
        "platform.identity.read", "platform.identity.manage", "platform.tenancy.read", "platform.tenancy.manage", "platform.organization.read",
        "platform.role.manage", "platform.module.view", "platform.settings.read", "platform.settings.manage", "platform.audit.view",
        "directory.party.read", "directory.party.create", "directory.party.edit", "directory.party.archive", "directory.party.role.manage",
        "directory.party.identifier.view", "directory.party.identifier.manage", "directory.contact.manage", "directory.address.manage",
        "directory.party.merge", "directory.party.visibility.manage", "directory.duplicate.override", "directory.import", "directory.export",
        "directory.audit.view", "directory.party.quick_create", "catalog.product.read", "catalog.product.create", "catalog.product.edit",
        "catalog.product.archive", "catalog.product.visibility.manage", "catalog.variant.manage", "catalog.product_uom.manage", "catalog.category.manage", "catalog.brand.manage", "catalog.uom.manage", "catalog.barcode.manage",
        "catalog.reference.manage", "catalog.attribute.manage", "catalog.import", "catalog.export", "catalog.audit.view", "finbase.currency.read", "finbase.currency.enable", "finbase.rate.read",
        "finbase.rate.manage", "finbase.rate.approve", "finbase.rounding_policy.read", "finbase.rounding_policy.manage", "finbase.payment_term.read",
        "finbase.payment_term.manage", "finbase.financial_policy.manage", "finbase.audit.view",
        "pricing.read", "pricing.list.manage", "pricing.version.create", "pricing.rule.manage", "pricing.version.activate", "pricing.simulate", "pricing.import", "pricing.export", "pricing.audit.view",
        "ledger.account.read", "ledger.account.manage", "ledger.journal.read", "ledger.journal.manage", "ledger.period.read", "ledger.period.manage", "ledger.period.soft_close", "ledger.period.close", "ledger.period.reopen", "ledger.lock.read", "ledger.lock.manage", "ledger.mapping.read", "ledger.mapping.manage", "ledger.mapping.activate", "ledger.entry.read", "ledger.entry.reverse", "ledger.entry.explain", "ledger.general_journal.create", "ledger.general_journal.edit", "ledger.general_journal.submit", "ledger.general_journal.approve", "ledger.general_journal.post", "ledger.dimension.read", "ledger.dimension.manage", "ledger.opening.read", "ledger.opening.manage", "ledger.opening.post", "ledger.reports.read", "ledger.audit.view", "ledger.configuration.read", "ledger.configuration.manage",
        "accounting.home.read", "accounting.owner.read", "accounting.health.read", "accounting.reconciliation.read", "accounting.reconciliation.run", "accounting.opening.approve", "accounting.close.preview", "accounting.close.execute", "accounting.close.reopen", "accounting.statement.read", "accounting.statement.manage", "accounting.fx-revaluation.read", "accounting.fx-revaluation.preview", "accounting.fx-revaluation.post", "accounting.fx-revaluation.reverse",
        "tax.read", "tax.registration.read", "tax.registration.manage", "tax.party_profile.read", "tax.party_profile.manage",
        "tax.product_profile.read", "tax.product_profile.manage", "tax.policy.read", "tax.policy.manage", "tax.rule.read",
        "tax.rule.manage", "tax.rule.activate", "tax.simulate", "tax.audit.view", "tax.import", "tax.export", "tax.capability.configure",
        "tax.country_pack.read", "tax.country_pack.manage", "tax.country_pack.activate",
        "subledger.read", "subledger.profile.read", "subledger.profile.manage", "subledger.credit_limit.read", "subledger.credit_limit.manage",
        "subledger.application.create", "subledger.application.reverse", "subledger.application.cross_currency", "subledger.advance.create",
        "subledger.credit-note.create", "subledger.aging.read", "subledger.statement.read", "subledger.reconciliation.read", "subledger.reconciliation.run",
        "subledger.writeoff.policy.manage", "subledger.writeoff.create", "subledger.writeoff.approve", "subledger.fx.manual-rate",
        "subledger.opening.manage", "subledger.opening.post", "subledger.opening.finalize",
        "invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize", "invoicing.customer_invoice.cancel_draft",
        "invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize",
        "invoicing.credit_note.create", "invoicing.debit_note.create", "invoicing.price_override", "invoicing.payment_term_override", "invoicing.date_override",
        "invoicing.accounting_preview", "invoicing.regulatory_details", "invoicing.attachment.manage", "invoicing.delivery.send", "invoicing.audit.view",
        "inventory.stock.read", "inventory.history.read", "inventory.cost.read", "inventory.valuation.read",
        "inventory.receipt.create", "inventory.receipt.confirm", "inventory.receipt.reverse", "inventory.operation.reverse", "inventory.issue.create", "inventory.issue.confirm", "inventory.issue.reverse", "inventory.stock.negative.override",
        "inventory.transfer.create", "inventory.transfer.ship", "inventory.transfer.receive", "inventory.transfer.reverse",
        "inventory.reservation.create", "inventory.reservation.release", "inventory.reservation.override", "inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post",
        "inventory.adjustment.create", "inventory.adjustment.approve", "inventory.adjustment.post", "inventory.revaluation.manage", "inventory.warehouse.manage", "inventory.location.manage",
        "inventory.profile.manage", "inventory.policy.manage", "inventory.traceability.manage", "inventory.opening.manage", "inventory.opening.post", "inventory.reconciliation.read", "inventory.audit.view",
        "hr.employee.read_basic", "hr.employee.create", "hr.employee.import", "hr.employee.manage", "hr.employee.archive", "hr.employee.link_user", "hr.employee.read_personal", "hr.employee.read_self", "hr.employee.read_team",
        "hr.employee.identifier.read", "hr.employee.identifier.manage", "hr.employee.identifier.reveal", "hr.employee.document.read", "hr.employee.document.read_self", "hr.employee.document.read_sensitive", "hr.employee.document.manage", "hr.employee.document.verify",
        "hr.employee.qualification.read", "hr.employee.qualification.manage", "hr.employee.credential.read", "hr.employee.credential.manage", "hr.employee.credential.verify", "hr.insurance.read", "hr.insurance.manage", "hr.insurance.reveal", "hr.compensation.read", "hr.compensation.manage", "hr.golive.manage", "hr.calendar.read", "hr.calendar.manage", "hr.shift.read", "hr.shift.manage", "hr.attendance.read", "hr.attendance.read_self", "hr.attendance.manage", "hr.attendance.correct", "hr.attendance.overtime.approve", "hr.attendance.payroll_lock", "hr.employment.read", "hr.employment.manage", "hr.assignment.read", "hr.assignment.manage",
        "hr.contract.read", "hr.contract.manage", "hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.offboarding.read", "hr.offboarding.manage", "hr.reminder.read", "hr.adjustment.read", "hr.adjustment.manage", "hr.adjustment.approve", "hr.leave.read", "hr.leave.read_self", "hr.leave.read_team", "hr.leave.approve_team", "hr.leave.request_self", "hr.leave.manage", "hr.leave.approve", "hr.leave.balance.adjust", "hr.onboarding.read", "hr.onboarding.manage", "hr.timeline.read", "hr.audit.read", "hr.export", "hr.home.read", "hr.report.read",
        "payroll.home.read", "payroll.group.read", "payroll.group.manage", "payroll.structure.read", "payroll.structure.manage", "payroll.component.read", "payroll.component.manage", "payroll.assignment.read", "payroll.assignment.manage", "payroll.input.read", "payroll.input.create", "payroll.input.approve", "payroll.run.read", "payroll.run.create", "payroll.run.capture_inputs", "payroll.run.calculate", "payroll.run.review", "payroll.run.approve", "payroll.run.finalize", "payroll.run.cancel", "payroll.payslip.read", "payroll.payslip.read_self", "payroll.payslip.export", "payroll.loan.read", "payroll.loan.create", "payroll.loan.approve", "payroll.loan.reschedule", "payroll.loan.writeoff", "payroll.retro.read", "payroll.retro.calculate", "payroll.offcycle.create", "payroll.payment.prepare", "payroll.payment.read", "payroll.payment.retry", "payroll.compliance_output.generate", "payroll.compliance_output.read", "payroll.accounting_preview.read", "payroll.settings.read", "payroll.settings.manage", "payroll.audit.read",
        "payments.read", "payments.incoming.create", "payments.outgoing.create", "payments.internal_transfer.create", "payments.complete_manual", "payments.cancel_pending", "payments.reverse", "payments.refund.create", "payments.refund.approve", "payments.account.read", "payments.account.manage", "payments.provider.read", "payments.provider.manage", "payments.provider.retry", "payments.provider.reconcile", "payments.cashbox.read", "payments.cashbox.use", "payments.cashbox.manage", "payments.fee.override", "payments.fx.override", "payments.date.override", "payments.audit.view"
    ];

    public static async Task<SeedIdentitySummary> SeedAsync(SeedContext context, string defaultPassword, string? protectedMfaSecret, string[]? mfaRecoveryDigests, CancellationToken cancellationToken)
    {
        var roles = await EnsureRolesAsync(context, cancellationToken);
        var users = BuildUsers(context.Tenant, context.Tenant.Id == SeedIds.AtlasTenant ? 205 : 105);
        var passwordHash = IdentityConventions.HashPassword(defaultPassword);

        await InsertIdentitiesAsync(context, users, passwordHash, cancellationToken);
        await InsertMembershipsAsync(context, users, cancellationToken);
        await InsertRoleAssignmentsAsync(context, users, roles, cancellationToken);

        var mfaEnabled = protectedMfaSecret is not null && mfaRecoveryDigests is { Length: > 0 };
        if (mfaEnabled)
        {
            var mfaUserId = SeedIds.Identity(context.Tenant.Id, "mfa");
            await context.ExecuteAsync("update platform.identity set mfa_secret_protected=$1,mfa_enabled=true,version=version+1 where tenant_id=$2 and id=$3 and status='Active'", cancellationToken, protectedMfaSecret, context.Tenant.Id, mfaUserId);
            foreach (var digest in mfaRecoveryDigests!)
                await context.ExecuteAsync("insert into platform.mfa_recovery_code (identity_id,digest,created_at) values ($1,$2,$3) on conflict (identity_id,digest) do update set consumed_at=null", cancellationToken, mfaUserId, digest, context.AsOf);
        }

        return new(users.Count, users.Count(user => user.Status == "Active"), users.Count(user => user.Status == "Suspended"), users.Count(user => user.MustChangePassword), mfaEnabled, users.Where(user => user.IsCanonical).Select(user => user.Email).ToArray());
    }

    private static async Task<Dictionary<string, Guid>> EnsureRolesAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var roleDefinitions = new (string Code, string Name, string[] Permissions)[]
        {
            ("administrator", "مدير النظام", Permissions),
            ("accountant", "محاسب", ["platform.identity.read", "platform.organization.read", "directory.party.read", "catalog.product.read", "finbase.currency.read", "finbase.rate.read", "finbase.rounding_policy.read", "finbase.payment_term.read", "pricing.read",
                "accounting.home.read", "accounting.owner.read", "accounting.health.read", "accounting.reconciliation.read", "accounting.reconciliation.run", "accounting.opening.approve", "accounting.close.preview", "accounting.statement.read", "accounting.fx-revaluation.read", "accounting.fx-revaluation.preview", "accounting.fx-revaluation.post", "accounting.fx-revaluation.reverse",
                "invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize", "invoicing.customer_invoice.cancel_draft",
                "invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize",
                "invoicing.credit_note.create", "invoicing.debit_note.create", "invoicing.price_override", "invoicing.payment_term_override", "invoicing.date_override",
                "invoicing.accounting_preview", "invoicing.regulatory_details", "invoicing.attachment.manage", "invoicing.audit.view"]),
            ("sales", "مبيعات", ["platform.organization.read", "directory.party.read", "directory.party.create", "directory.party.edit", "catalog.product.read", "finbase.currency.read", "finbase.payment_term.read", "pricing.read", "pricing.simulate",
                "invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.cancel_draft"]),
            ("warehouse", "مخازن", ["platform.organization.read", "catalog.product.read", "catalog.product.edit", "catalog.uom.manage"]),
            ("auditor", "مراجع مالي", ["platform.organization.read", "accounting.home.read", "accounting.owner.read", "accounting.health.read", "accounting.reconciliation.read", "accounting.statement.read", "accounting.fx-revaluation.read", "ledger.account.read", "ledger.journal.read", "ledger.period.read", "ledger.reports.read", "ledger.audit.view"]),
            ("office", "موظف إداري", ["platform.organization.read", "directory.party.read", "catalog.product.read", "finbase.currency.read"]),
            ("hr_employee_self", "خدماتي الوظيفية", ["platform.organization.read", "hr.employee.read_self", "hr.employee.document.read_self", "hr.attendance.read_self", "hr.leave.read_self", "hr.leave.request_self"]),
            ("hr_manager", "مدير فريق", ["platform.organization.read", "hr.employee.read_team", "hr.leave.read_team", "hr.leave.approve_team"]),
            ("hr_officer", "مسؤول الموارد البشرية", ["platform.organization.read", "hr.employee.read_basic", "hr.employee.create", "hr.employee.import", "hr.employee.manage", "hr.employment.read", "hr.employment.manage", "hr.assignment.read", "hr.assignment.manage", "hr.contract.read", "hr.contract.manage", "hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.offboarding.read", "hr.offboarding.manage", "hr.reminder.read", "hr.adjustment.read", "hr.adjustment.manage", "hr.leave.read", "hr.leave.manage", "hr.onboarding.read", "hr.onboarding.manage", "hr.timeline.read", "hr.home.read", "hr.report.read", "hr.employee.identifier.read", "hr.employee.identifier.manage", "hr.employee.document.read", "hr.employee.document.manage", "hr.employee.document.verify", "hr.employee.qualification.read", "hr.employee.qualification.manage", "hr.employee.credential.read", "hr.employee.credential.manage", "hr.employee.credential.verify", "hr.calendar.read", "hr.calendar.manage", "hr.shift.read", "hr.shift.manage", "hr.attendance.read", "hr.attendance.manage"]),
            ("hr_privacy", "مسؤول بيانات الموارد البشرية", ["platform.organization.read", "hr.employee.read_basic", "hr.employee.create", "hr.employee.import", "hr.employee.manage", "hr.employee.link_user", "hr.employee.read_personal", "hr.employment.read", "hr.employment.manage", "hr.assignment.read", "hr.assignment.manage", "hr.contract.read", "hr.contract.manage", "hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.offboarding.read", "hr.offboarding.manage", "hr.reminder.read", "hr.adjustment.read", "hr.adjustment.approve", "hr.leave.read", "hr.leave.approve", "hr.leave.balance.adjust", "hr.timeline.read", "hr.home.read", "hr.report.read", "hr.employee.identifier.read", "hr.employee.identifier.manage", "hr.employee.identifier.reveal", "hr.employee.document.read", "hr.employee.document.read_sensitive", "hr.employee.document.manage", "hr.employee.document.verify", "hr.employee.qualification.read", "hr.employee.qualification.manage", "hr.employee.credential.read", "hr.employee.credential.manage", "hr.employee.credential.verify", "hr.insurance.read", "hr.insurance.manage", "hr.insurance.reveal", "hr.compensation.read", "hr.compensation.manage", "hr.golive.manage", "hr.calendar.read", "hr.calendar.manage", "hr.shift.read", "hr.shift.manage", "hr.attendance.read", "hr.attendance.manage", "hr.attendance.correct", "hr.attendance.overtime.approve", "hr.attendance.payroll_lock", "hr.audit.read", "hr.export"])
        };
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var role in roleDefinitions)
        {
            var id = SeedIds.Role(context.Tenant.Id, role.Code);
            result[role.Code] = id;
            await context.ExecuteAsync("insert into platform.role (id,tenant_id,code,name,is_system_template,is_active,version) values ($1,$2,$3,$4,false,true,1) on conflict (id) do update set code=excluded.code,name=excluded.name,is_active=true,version=platform.role.version+1", cancellationToken, id, context.Tenant.Id, role.Code, role.Name);
            if (role.Code == "sales")
                await context.ExecuteAsync("delete from platform.role_permission where tenant_id=$1 and role_id=$2 and permission_code='invoicing.accounting_preview'", cancellationToken, context.Tenant.Id, id);
            foreach (var permission in role.Permissions.Distinct(StringComparer.Ordinal))
                await context.ExecuteAsync("insert into platform.role_permission (tenant_id,role_id,permission_code) values ($1,$2,$3) on conflict do nothing", cancellationToken, context.Tenant.Id, id, permission);
        }
        return result;
    }

    private static async Task InsertIdentitiesAsync(SeedContext context, IReadOnlyList<UserSpec> users, string passwordHash, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("insert into platform.identity (id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) select * from unnest($1::uuid[],$2::uuid[],$3::text[],$4::text[],$5::text[],$6::text[],$7::boolean[],$8::text[],$9::boolean[],$10::text[],$11::timestamptz[],$12::uuid[],$13::bigint[]) on conflict (id) do update set email=excluded.email,normalized_email=excluded.normalized_email,login_prefix=excluded.login_prefix,name=excluded.name,email_verified=true,must_change_password=excluded.must_change_password,status=excluded.status,version=platform.identity.version+1", context.Connection, context.Transaction);
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => user.IdentityId).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(_ => context.Tenant.Id).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Email).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => IdentityConventions.NormalizeEmail(user.Email)).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Prefix).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Name).ToArray());
        AddArray(command, NpgsqlDbType.Boolean, users.Select(_ => true).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(_ => passwordHash).ToArray());
        AddArray(command, NpgsqlDbType.Boolean, users.Select(user => user.MustChangePassword).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Status).ToArray());
        AddArray(command, NpgsqlDbType.TimestampTz, users.Select(_ => context.AsOf).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => SeedIds.Stable($"{user.IdentityId:D}:security")).ToArray());
        AddArray(command, NpgsqlDbType.Bigint, users.Select(_ => 1L).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMembershipsAsync(SeedContext context, IReadOnlyList<UserSpec> users, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("insert into platform.membership (id,tenant_id,identity_id,display_name,status,default_legal_entity_id,default_branch_id,locale,timezone,created_at,version) select * from unnest($1::uuid[],$2::uuid[],$3::uuid[],$4::text[],$5::text[],$6::uuid[],$7::uuid[],$8::text[],$9::text[],$10::timestamptz[],$11::bigint[]) on conflict (id) do update set display_name=excluded.display_name,status=excluded.status,default_legal_entity_id=excluded.default_legal_entity_id,default_branch_id=excluded.default_branch_id,locale=excluded.locale,timezone=excluded.timezone,version=platform.membership.version+1", context.Connection, context.Transaction);
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => user.MembershipId).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(_ => context.Tenant.Id).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => user.IdentityId).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Name).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(user => user.Status).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(_ => context.EntityId).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(_ => context.BranchId).ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(_ => "ar-EG").ToArray());
        AddArray(command, NpgsqlDbType.Text, users.Select(_ => "Africa/Cairo").ToArray());
        AddArray(command, NpgsqlDbType.TimestampTz, users.Select(_ => context.AsOf).ToArray());
        AddArray(command, NpgsqlDbType.Bigint, users.Select(_ => 1L).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRoleAssignmentsAsync(SeedContext context, IReadOnlyList<UserSpec> users, Dictionary<string, Guid> roles, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("insert into platform.membership_role_assignment (tenant_id,membership_id,role_id,scope_type,scope_id) select $1,membership_id,role_id,'Tenant',null from unnest($2::uuid[],$3::uuid[]) as rows(membership_id,role_id) on conflict (tenant_id,membership_id,role_id,scope_type,scope_id) do nothing", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(context.Tenant.Id);
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => user.MembershipId).ToArray());
        AddArray(command, NpgsqlDbType.Uuid, users.Select(user => roles[user.RoleCode]).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<UserSpec> BuildUsers(SeedTenant tenant, int count)
    {
        var users = new List<UserSpec>();
        var canonical = new List<(string Prefix, string Name, string RoleCode, string Status, bool MustChangePassword, bool IsCanonical)>
        {
            new("admin", tenant.Id == SeedIds.AtlasTenant ? "مدير شركة أطلس" : "مدير شركة دلتا", "administrator", "Active", false, true),
            new("accountant", "محمود عبدالسلام", "accountant", "Active", false, true),
            new("sales", "سارة حمدي", "sales", "Active", false, true),
            new("warehouse", "خالد السيد", "warehouse", "Active", false, true),
            new("office", "نورهان مصطفى", "office", "Active", false, true),
            new("hr", "هالة إبراهيم", "hr_officer", "Active", false, true),
            new("hr-privacy", "منال عبدالرحمن", "hr_privacy", "Active", false, true),
            new("hr-manager", "محمود المدير", "hr_manager", "Active", false, true),
            new("inactive", "ياسر فوزي", "office", "Suspended", false, true),
            new("temporary", "أحمد مؤقت", "office", "Active", true, true)
        };
        if (tenant.Id == SeedIds.AtlasTenant) canonical.Add(("mfa", "مريم حماية الحساب", "office", "Active", false, true));
        foreach (var user in canonical) users.Add(CreateUser(tenant, user.Prefix, user.Name, user.RoleCode, user.Status, user.MustChangePassword, true));

        var names = new[] { "أحمد محمود", "سارة إبراهيم", "محمد عادل", "مريم حسن", "خالد سامي", "نور علي", "عمر شريف", "هبة مصطفى", "Youssef Nabil", "Mona Adel", "Omar Hassan", "هاني إبراهيم" };
        var departments = new[] { "المشتريات", "المبيعات", "المخازن", "الحسابات", "خدمة العملاء", "التشغيل" };
        for (var index = users.Count; index < count; index++)
        {
            var sequence = index + 1;
            var prefix = $"staff-{sequence:000}";
            var name = $"{names[index % names.Length]} — {departments[index % departments.Length]}";
            users.Add(CreateUser(tenant, prefix, name, index % 6 == 0 ? "sales" : index % 5 == 0 ? "warehouse" : "office", "Active", false, false));
        }
        return users;
    }

    private static UserSpec CreateUser(SeedTenant tenant, string prefix, string name, string roleCode, string status, bool mustChangePassword, bool canonical)
    {
        var identityId = SeedIds.Identity(tenant.Id, prefix);
        return new(identityId, SeedIds.Membership(tenant.Id, prefix), prefix, LoginDomainConventions.BuildEmail(prefix, tenant.LoginDomain), name, roleCode, status, mustChangePassword, canonical);
    }

    private static void AddArray<T>(NpgsqlCommand command, NpgsqlDbType type, T[] values)
    {
        command.Parameters.AddWithValue(type | NpgsqlDbType.Array, values);
    }

    private sealed record UserSpec(Guid IdentityId, Guid MembershipId, string Prefix, string Email, string Name, string RoleCode, string Status, bool MustChangePassword, bool IsCanonical);
}

public sealed record SeedIdentitySummary(int Total, int Active, int Suspended, int MustChangePassword, bool MfaEnabled, IReadOnlyList<string> CanonicalEmails);
