using Core.Authorization;
using Core.Services;
using Core.Transactions;
using Modules.Subledger.Entities;
using Modules.Subledger.Repositories;
using Modules.Subledger.Services;
using Npgsql;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Subledger.Tests;

public sealed class SubledgerAuthorizationTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId OtherTenant = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
    private static readonly LegalEntityId Entity = new(Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528"));
    private static readonly Guid PartyId = Guid.Parse("10000000-0000-7000-8000-000000000001");

    [Fact]
    public async Task SensitiveCommandsRequireTheirOwnPermission()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused");
        var membershipId = Guid.CreateVersion7();
        var service = CreateService(dataSource, new ScopedPermissionSubjectStore(Tenant, membershipId, Entity, []));
        var actor = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();
        var itemId = Guid.CreateVersion7();
        var otherItemId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var batchId = Guid.CreateVersion7();

        var commands = new (string Permission, Func<Task> Invoke)[]
        {
            ("subledger.profile.manage", async () => await service.UpdateFinancialProfileAsync(Tenant, membershipId,
                new(Entity, PartyId, null, null, null, null, null, null, 0, actor, correlation))),
            ("subledger.credit_limit.read", async () => await service.GetPartyBalanceForMemberAsync(Tenant, membershipId,
                new(Entity, PartyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25)))),
            ("subledger.application.create", async () => await service.ApplyAsync(Tenant, membershipId,
                new(Entity, PartyId, SubledgerDirectionContract.Receivable, "auth-apply", new(2026, 10, 25), new(2026, 10, 25),
                    [new(itemId, otherItemId, "1", ExpectedDebitVersion: 1, ExpectedCreditVersion: 1)], actor, correlation))),
            ("subledger.application.reverse", async () => await service.ReverseAsync(Tenant, membershipId,
                new(Entity, itemId, "auth-reverse", new(2026, 10, 25), "authorized test", actor, correlation))),
            ("subledger.writeoff.policy.manage", async () => await service.UpdateWriteOffPolicyAsync(Tenant, membershipId,
                new(Entity, SubledgerDirectionContract.Receivable, "EGP", "10", "2", 0, actor, correlation))),
            ("subledger.writeoff.create", async () => await service.CreateWriteOffRequestAsync(Tenant, membershipId,
                new(Entity, itemId, "1", "ROUNDING", "Authorization test", new(2026, 10, 25), "auth-writeoff", actor, correlation))),
            ("subledger.writeoff.approve", async () => await service.ApproveWriteOffAsync(Tenant, membershipId,
                new(Entity, requestId, actor, correlation))),
            ("subledger.reconciliation.run", async () => await service.ReconcileControlBalancesAsync(Tenant, membershipId,
                new(Entity, new(2026, 10, 25)), actor, correlation)),
            ("subledger.reconciliation.read", async () => await service.GetReconciliationControlAccountsAsync(Tenant, membershipId,
                new(Entity, new(2026, 10, 25)))),
            ("subledger.reconciliation.read", async () => await service.GetReconciliationRunAsync(Tenant, membershipId, Entity, requestId)),
            ("subledger.opening.manage", async () => await service.CreateOpeningSubledgerBatchAsync(Tenant, membershipId,
                new(Entity, "auth-opening", new(2026, 10, 25), "EGP", "configuration:1", "auth-opening-key", actor, correlation))),
            ("subledger.opening.manage", async () => await service.AddOpeningSubledgerLinesAsync(Tenant, membershipId,
                Entity, batchId, [], actor, correlation)),
            ("subledger.opening.manage", async () => await service.ValidateOpeningSubledgerBatchAsync(Tenant, membershipId, Entity, batchId)),
            ("subledger.opening.finalize", async () => await service.FinalizeOpeningSubledgerBatchAsync(Tenant, membershipId,
                Entity, batchId, actor, correlation))
        };

        foreach (var command in commands)
        {
            var exception = await Assert.ThrowsAsync<SubledgerException>(command.Invoke);
            Assert.Equal("subledger.permission_denied", exception.Code);
            Assert.False(string.IsNullOrWhiteSpace(command.Permission));
        }
    }

    [Fact]
    public async Task ReconciliationReadIsScopedToTenantAndLegalEntity()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused");
        var membershipId = Guid.CreateVersion7();
        var service = CreateService(dataSource, new ScopedPermissionSubjectStore(Tenant, membershipId, Entity, ["subledger.reconciliation.read"]));

        var wrongEntity = await Assert.ThrowsAsync<SubledgerException>(() => service.GetReconciliationControlAccountsAsync(
            Tenant, membershipId, new(new LegalEntityId(Guid.CreateVersion7()), new(2026, 10, 25))));
        var wrongTenant = await Assert.ThrowsAsync<SubledgerException>(() => service.GetReconciliationControlAccountsAsync(
            OtherTenant, membershipId, new(Entity, new(2026, 10, 25))));

        Assert.Equal("subledger.permission_denied", wrongEntity.Code);
        Assert.Equal("subledger.permission_denied", wrongTenant.Code);
    }

    [Fact]
    public async Task SalesCreditLimitPermissionAllowsReceivableExposureButNotPayablesOrOtherEntities()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused");
        var membershipId = Guid.CreateVersion7();
        var service = new SubledgerService(new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System),
            new ActivePartyDirectory(), null!, new FixedEntityCurrency(),
            new ScopedPermissionSubjectStore(Tenant, membershipId, Entity, ["subledger.credit_limit.read"]), new PermissionAuthorizer(), TimeProvider.System);

        await Assert.ThrowsAsync<NpgsqlException>(() => service.GetPartyBalanceForMemberAsync(Tenant, membershipId,
            new(Entity, PartyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25))));

        var payable = await Assert.ThrowsAsync<SubledgerException>(() => service.GetPartyBalanceForMemberAsync(Tenant, membershipId,
            new(Entity, PartyId, SubledgerDirectionContract.Payable, new(2026, 10, 25))));
        var wrongEntity = await Assert.ThrowsAsync<SubledgerException>(() => service.GetPartyBalanceForMemberAsync(Tenant, membershipId,
            new(new LegalEntityId(Guid.CreateVersion7()), PartyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25))));
        var wrongTenant = await Assert.ThrowsAsync<SubledgerException>(() => service.GetPartyBalanceForMemberAsync(OtherTenant, membershipId,
            new(Entity, PartyId, SubledgerDirectionContract.Receivable, new(2026, 10, 25))));

        Assert.Equal("subledger.permission_denied", payable.Code);
        Assert.Equal("subledger.permission_denied", wrongEntity.Code);
        Assert.Equal("subledger.permission_denied", wrongTenant.Code);
    }

    private static SubledgerService CreateService(NpgsqlDataSource dataSource, IAuthorizationSubjectStore subjects) =>
        new(new SubledgerRepository(dataSource, new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System), null!, null!, null!,
            subjects, new PermissionAuthorizer(), TimeProvider.System);

    private sealed class ActivePartyDirectory : Shared.Contracts.Directory.IDirectoryCapabilities
    {
        public Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(partyIds.ToHashSet());
        public Task<Shared.Contracts.Directory.InvoicePartySnapshotContract?> ReadInvoiceSnapshotAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<Shared.Contracts.Directory.InvoicePartySnapshotContract?>(null);
    }

    private sealed class FixedEntityCurrency : ILegalEntityFinancialReadStore
    {
        public Task<string?> GetCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("TEST");
        public Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("EGP");
        public Task<string?> GetTimeZoneAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Africa/Cairo");
    }

    private sealed class ScopedPermissionSubjectStore(TenantId tenant, Guid membership, LegalEntityId entity, IReadOnlyList<string> permissionCodes)
        : IAuthorizationSubjectStore
    {
        public Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default)
        {
            if (tenantId != tenant || membershipId != membership) return Task.FromResult<AuthorizationSubject?>(null);
            var roleId = Guid.CreateVersion7();
            var role = new Role(roleId, tenant, "subledger-test", "Subledger Test", false, true, 1);
            var assignment = new MembershipRoleAssignment(tenant, membership, roleId, PermissionScope.ForLegalEntity(tenant, entity));
            var permissions = permissionCodes.Select(code => new RolePermissionEntry(tenant, roleId, code)).ToArray();
            return Task.FromResult<AuthorizationSubject?>(new(tenant, membership, [assignment], [role], permissions, 1));
        }
    }
}
