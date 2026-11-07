using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Catalog;
using Shared.Contracts.Pricing;
using Shared.Contracts.Tax;
using Shared.Contracts.Payments;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Subledger;
using Todir.Sales.Contracts;

namespace Modules.POS;

public sealed class PosService(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource, ICatalogCapabilities catalog, IPricingCapabilities pricing, ITaxCapabilities tax, IPosPaymentCapabilities payments, IPosInvoicingCapabilities invoicing, IInvoiceComplianceSnapshotCapability snapshots, ISubledgerCapabilities subledger, ISalesInventoryCapabilities inventory, IQuantityConversion quantityConversion)
{
    private static readonly string[] OfflineCapabilities = ["CashSale", "ReceiptPrint", "QueuedSync"];
    private static readonly JsonSerializerOptions OfflineJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> CreateManagerOverrideAsync(TenantId tenant, Guid actor, ManagerOverrideRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Scope, "Discount", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.Reason) || request.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("pos.override_invalid");
        var id = Guid.CreateVersion7();
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_pos.pos_manager_override(tenant_id,id,scope,target_id,reason,approved_by,expires_at,created_at) values($1,$2,$3,$4,$5,$6,$7,$8)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.Scope.Trim()); command.Parameters.AddWithValue(request.TargetId); command.Parameters.AddWithValue(request.Reason.Trim()); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(request.ExpiresAt); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); await command.ExecuteNonQueryAsync(token);
            await NpgsqlAuditWriter.WriteAsync(ctx.Connection, ctx.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "Human", actor, null, "pos", "manager_override_created", "manager_override", id, null, JsonSerializer.Serialize(new { request.Scope, request.TargetId, request.ExpiresAt }), request.Reason.Trim(), "pos-api", id, System.Diagnostics.Activity.Current?.Id, DateTimeOffset.UtcNow, null), token);
            return null;
        }, ct);
        return id;
    }

    public async Task<RegisterResponse> CreateRegisterAsync(TenantId tenant, Guid actor, Guid entityId, CreateRegisterRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId != entityId || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("pos.register_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var branch = new NpgsqlCommand("select 1 from platform.branch where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status='Active'", ctx.Connection, ctx.Transaction);
            branch.Parameters.AddWithValue(tenant.Value); branch.Parameters.AddWithValue(request.BranchId); branch.Parameters.AddWithValue(entityId);
            if (await branch.ExecuteScalarAsync(token) is null) throw new ArgumentException("pos.branch_not_in_legal_entity");
            await using var command = new NpgsqlCommand("insert into business_pos.pos_register(tenant_id,id,legal_entity_id,branch_id,code,name,currency_code,offline_enabled,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$10)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(request.BranchId); command.Parameters.AddWithValue(request.Code.Trim()); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue(request.CurrencyCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue(request.OfflineEnabled); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(now);
            await command.ExecuteNonQueryAsync(token);
            return null;
        }, ct);
        return new(id, entityId, request.BranchId, request.Code.Trim(), request.Name.Trim(), request.CurrencyCode.Trim().ToUpperInvariant(), "Active", request.OfflineEnabled);
    }

    public async Task<IReadOnlyList<RegisterResponse>> ListRegistersAsync(TenantId tenant, Guid entityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,branch_id,code,name,currency_code,status,offline_enabled from business_pos.pos_register where tenant_id=$1 and legal_entity_id=$2 order by code limit 200", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId);
        var result = new List<RegisterResponse>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetBoolean(7)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct); return result;
    }

    public async Task<RegisterResponse> UpdateRegisterAsync(TenantId tenant, Guid registerId, UpdateRegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CurrencyCode))
            throw new ArgumentException("pos.register_invalid");
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_register set name=$3,currency_code=$4,offline_enabled=$5,updated_at=$6,row_version=row_version+1 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(registerId); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue(request.CurrencyCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue(request.OfflineEnabled); command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new ArgumentException("pos.register_not_found");
            return null;
        }, ct);
        return await GetRegisterAsync(tenant, registerId, ct);
    }

    public async Task<RegisterResponse> GetRegisterAsync(TenantId tenant, Guid registerId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,branch_id,code,name,currency_code,status,offline_enabled from business_pos.pos_register where tenant_id=$1 and id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(registerId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.register_not_found"); var result = new RegisterResponse(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetBoolean(7)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<PosResourceScope?> ResolveResourceScopeAsync(TenantId tenant, string resourceType, Guid resourceId, CancellationToken ct)
    {
        const string register = "select legal_entity_id,branch_id from business_pos.pos_register where tenant_id=$1 and id=$2";
        const string terminal = "select r.legal_entity_id,r.branch_id from business_pos.pos_terminal t join business_pos.pos_register r on r.tenant_id=t.tenant_id and r.id=t.register_id where t.tenant_id=$1 and t.id=$2";
        const string session = "select r.legal_entity_id,r.branch_id from business_pos.pos_session s join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where s.tenant_id=$1 and s.id=$2";
        const string cart = "select r.legal_entity_id,r.branch_id from business_pos.pos_cart c join business_pos.pos_session s on s.tenant_id=c.tenant_id and s.id=c.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where c.tenant_id=$1 and c.id=$2";
        const string checkout = "select r.legal_entity_id,r.branch_id from business_pos.pos_checkout ch join business_pos.pos_cart c on c.tenant_id=ch.tenant_id and c.id=ch.cart_id join business_pos.pos_session s on s.tenant_id=c.tenant_id and s.id=c.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where ch.tenant_id=$1 and ch.id=$2";
        const string @return = "select r.legal_entity_id,r.branch_id from business_pos.pos_return pr join business_pos.pos_session s on s.tenant_id=pr.tenant_id and s.id=pr.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where pr.tenant_id=$1 and pr.id=$2";
        const string exchange = "select r.legal_entity_id,r.branch_id from business_pos.pos_exchange_group eg join business_pos.pos_return pr on pr.tenant_id=eg.tenant_id and pr.id=eg.return_id join business_pos.pos_session s on s.tenant_id=pr.tenant_id and s.id=pr.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where eg.tenant_id=$1 and eg.id=$2";
        const string offlineEnvelope = "select r.legal_entity_id,r.branch_id from business_pos.pos_offline_envelope oe join business_pos.pos_register r on r.tenant_id=oe.tenant_id and r.id=oe.register_id where oe.tenant_id=$1 and oe.id=$2";
        const string offlineConflict = "select r.legal_entity_id,r.branch_id from business_pos.pos_sync_conflict sc join business_pos.pos_offline_envelope oe on oe.tenant_id=sc.tenant_id and oe.id=sc.envelope_id join business_pos.pos_register r on r.tenant_id=oe.tenant_id and r.id=oe.register_id where sc.tenant_id=$1 and sc.id=$2";
        var sql = resourceType switch
        {
            "register" => register,
            "terminal" => terminal,
            "session" => session,
            "cart" => cart,
            "checkout" => checkout,
            "return" => @return,
            "exchange" => exchange,
            "offline-envelope" => offlineEnvelope,
            "offline-conflict" => offlineConflict,
            _ => throw new ArgumentException("pos.resource_type_invalid")
        };

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(resourceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            await transaction.CommitAsync(ct);
            return null;
        }

        var scope = new PosResourceScope(reader.GetGuid(0), reader.GetGuid(1));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return scope;
    }

    public async Task<TerminalResponse> RegisterTerminalAsync(TenantId tenant, Guid registerId, RegisterTerminalRequest request, CancellationToken ct)
    {
        if (request.RegisterId != registerId || string.IsNullOrWhiteSpace(request.DeviceIdentity) || string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("pos.terminal_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var register = new NpgsqlCommand("select 1 from business_pos.pos_register where tenant_id=$1 and id=$2 and status='Active'", ctx.Connection, ctx.Transaction);
            register.Parameters.AddWithValue(tenant.Value); register.Parameters.AddWithValue(registerId);
            if (await register.ExecuteScalarAsync(token) is null) throw new ArgumentException("pos.register_not_ready");
            await using var command = new NpgsqlCommand("insert into business_pos.pos_terminal(tenant_id,id,register_id,device_identity,display_name,status,created_at,updated_at) values($1,$2,$3,$4,$5,'Ready',$6,$6)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(registerId); command.Parameters.AddWithValue(request.DeviceIdentity.Trim()); command.Parameters.AddWithValue(request.DisplayName.Trim()); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); return null;
        }, ct);
        return new(id, registerId, request.DeviceIdentity.Trim(), request.DisplayName.Trim(), "Ready", 1, now);
    }

    public async Task<RegisterResponse> ChangeRegisterStatusAsync(TenantId tenant, Guid registerId, string status, CancellationToken ct)
    {
        if (status is not ("Active" or "Disabled" or "Retiring")) throw new ArgumentException("pos.register_status_invalid");
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var command = new NpgsqlCommand("update business_pos.pos_register set status=$3,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(registerId); command.Parameters.AddWithValue(status); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); if (await command.ExecuteNonQueryAsync(token) != 1) throw new ArgumentException("pos.register_not_found"); return null; }, ct);
        return await GetRegisterAsync(tenant, registerId, ct);
    }

    public async Task<TerminalResponse> ChangeTerminalAsync(TenantId tenant, Guid terminalId, string operation, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            var sql = operation switch
            {
                "revoke" => "update business_pos.pos_terminal set status='Revoked',revoked_at=$3,row_version=row_version+1,updated_at=$3 where tenant_id=$1 and id=$2",
                "heartbeat" => "update business_pos.pos_terminal set last_seen_at=$3,updated_at=$3 where tenant_id=$1 and id=$2 and status='Ready'",
                "rotate" => "update business_pos.pos_terminal set credential_version=credential_version+1,status='Ready',revoked_at=null,row_version=row_version+1,updated_at=$3 where tenant_id=$1 and id=$2",
                _ => throw new ArgumentException("pos.terminal_operation_invalid")
            };
            await using var command = new NpgsqlCommand(sql, ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(terminalId); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.terminal_not_ready"); return null;
        }, ct);
        return await GetTerminalAsync(tenant, terminalId, ct);
    }

    private async Task<TerminalResponse> GetTerminalAsync(TenantId tenant, Guid terminalId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,register_id,device_identity,display_name,status,credential_version,last_seen_at from business_pos.pos_terminal where tenant_id=$1 and id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(terminalId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.terminal_not_found"); var result = new TerminalResponse(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<SessionResponse> OpenSessionAsync(TenantId tenant, Guid actor, Guid entityId, OpenSessionRequest request, CancellationToken ct)
    {
        if (request.OpeningFloat < 0) throw new ArgumentException("pos.opening_float_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        try
        {
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
            {
                await using var register = new NpgsqlCommand("select currency_code from business_pos.pos_register where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status='Active'", ctx.Connection, ctx.Transaction); register.Parameters.AddWithValue(tenant.Value); register.Parameters.AddWithValue(request.RegisterId); register.Parameters.AddWithValue(entityId); var registerCurrency = (string?)await register.ExecuteScalarAsync(token); if (registerCurrency is null) throw new ArgumentException("pos.register_not_ready"); if (!string.Equals(registerCurrency, request.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("pos.session_currency_invalid");
                if (request.TerminalId is { } terminalId)
                {
                    await using var terminal = new NpgsqlCommand("select 1 from business_pos.pos_terminal where tenant_id=$1 and id=$2 and register_id=$3 and status='Ready'", ctx.Connection, ctx.Transaction); terminal.Parameters.AddWithValue(tenant.Value); terminal.Parameters.AddWithValue(terminalId); terminal.Parameters.AddWithValue(request.RegisterId); if (await terminal.ExecuteScalarAsync(token) is null) throw new ArgumentException("pos.terminal_not_ready");
                }
                await using var command = new NpgsqlCommand("insert into business_pos.pos_session(tenant_id,id,register_id,terminal_id,cashier_user_id,business_date,opened_at,status,opening_float,expected_cash,row_version) values($1,$2,$3,$4,$5,$6,$7,'Open',$8,0,1)", ctx.Connection, ctx.Transaction);
                command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.RegisterId); command.Parameters.AddWithValue((object?)request.TerminalId ?? DBNull.Value); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(request.BusinessDate); command.Parameters.AddWithValue(now); command.Parameters.AddWithValue(request.OpeningFloat);
                await command.ExecuteNonQueryAsync(token);
                if (request.OpeningFloat > 0) await AddCashMovementInContextAsync(ctx, tenant, id, actor, "OpeningFloat", request.OpeningFloat, request.CurrencyCode, "Opening float", token);
                return null;
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) { throw new InvalidOperationException("pos.session_already_open"); }
        return new(id, request.RegisterId, request.TerminalId, request.BusinessDate, "Open", request.OpeningFloat, null, null);
    }

    public async Task<Guid> AddCashMovementAsync(TenantId tenant, Guid actor, Guid sessionId, CashMovementRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("pos.cash_movement_invalid");
        var id = Guid.CreateVersion7();
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await AddCashMovementInContextAsync(ctx, tenant, sessionId, actor, request.MovementType, request.Amount, request.CurrencyCode, request.Reason, token, id); return null; }, ct);
        return id;
    }

    public async Task<SessionResponse> CloseSessionAsync(TenantId tenant, Guid actor, Guid sessionId, CloseSessionRequest request, CancellationToken ct)
    {
        if (request.CountedCash < 0) throw new ArgumentException("pos.counted_cash_invalid");
        return await coordinator.ExecuteAsync<SessionResponse>(tenant, async (ctx, token) =>
        {
            await using var sessionLock = new NpgsqlCommand("select status from business_pos.pos_session where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            sessionLock.Parameters.AddWithValue(tenant.Value); sessionLock.Parameters.AddWithValue(sessionId);
            if (await sessionLock.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.session_not_open");
            await using var blockers = new NpgsqlCommand("select exists(select 1 from business_pos.pos_cart where tenant_id=$1 and session_id=$2 and state='Held'), exists(select 1 from business_pos.pos_checkout where tenant_id=$1 and cart_id in (select id from business_pos.pos_cart where tenant_id=$1 and session_id=$2) and state not in ('Completed','PaymentFailed'))", ctx.Connection, ctx.Transaction);
            blockers.Parameters.AddWithValue(tenant.Value); blockers.Parameters.AddWithValue(sessionId); await using var blockerReader = await blockers.ExecuteReaderAsync(token); await blockerReader.ReadAsync(token); var held = blockerReader.GetBoolean(0); var pending = blockerReader.GetBoolean(1); await blockerReader.CloseAsync(); if (held) throw new InvalidOperationException("pos.session_has_held_cart"); if (pending) throw new InvalidOperationException("pos.session_has_pending_checkout");
            await using var expectedCommand = new NpgsqlCommand("select expected_cash from business_pos.pos_session where tenant_id=$1 and id=$2 and status in ('Open','Reconciling')", ctx.Connection, ctx.Transaction); expectedCommand.Parameters.AddWithValue(tenant.Value); expectedCommand.Parameters.AddWithValue(sessionId); var expectedValue = await expectedCommand.ExecuteScalarAsync(token); if (expectedValue is not decimal expectedCash) throw new InvalidOperationException("pos.session_not_open"); if (request.CountedCash != expectedCash && string.IsNullOrWhiteSpace(request.VarianceReason)) throw new ArgumentException("pos.variance_reason_required");
            await using var command = new NpgsqlCommand("update business_pos.pos_session set status=case when $3=expected_cash then 'Closed' else 'Reconciling' end,closed_at=$4,counted_cash=$3,variance=$3-expected_cash,variance_reason=$5,row_version=row_version+1 where tenant_id=$1 and id=$2 and status in ('Open','Reconciling') returning register_id,terminal_id,business_date,opening_float,status,counted_cash,variance", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(request.CountedCash); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); command.Parameters.AddWithValue((object?)request.VarianceReason ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.session_not_open");
            var response = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(4), reader.GetDecimal(3), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6));
            await reader.CloseAsync();
            return response;
        }, ct);
    }

    public async Task<SessionResponse> StartSessionCloseAsync(TenantId tenant, Guid sessionId, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync<SessionResponse>(tenant, async (ctx, token) =>
        {
            await using var sessionLock = new NpgsqlCommand("select status from business_pos.pos_session where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            sessionLock.Parameters.AddWithValue(tenant.Value); sessionLock.Parameters.AddWithValue(sessionId);
            if (!string.Equals((string?)await sessionLock.ExecuteScalarAsync(token), "Open", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.session_not_open");
            await using var blockers = new NpgsqlCommand("select exists(select 1 from business_pos.pos_cart where tenant_id=$1 and session_id=$2 and state='Held'), exists(select 1 from business_pos.pos_checkout where tenant_id=$1 and cart_id in (select id from business_pos.pos_cart where tenant_id=$1 and session_id=$2) and state not in ('Completed','PaymentFailed'))", ctx.Connection, ctx.Transaction);
            blockers.Parameters.AddWithValue(tenant.Value); blockers.Parameters.AddWithValue(sessionId);
            await using var blockerReader = await blockers.ExecuteReaderAsync(token);
            await blockerReader.ReadAsync(token);
            var held = blockerReader.GetBoolean(0);
            var pending = blockerReader.GetBoolean(1);
            await blockerReader.CloseAsync();
            if (held) throw new InvalidOperationException("pos.session_has_held_cart");
            if (pending) throw new InvalidOperationException("pos.session_has_pending_checkout");
            await using var command = new NpgsqlCommand("update business_pos.pos_session set status='Reconciling',row_version=row_version+1 where tenant_id=$1 and id=$2 and status='Open' returning register_id,terminal_id,business_date,opening_float,status,counted_cash,variance", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.session_not_open");
            var response = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(4), reader.GetDecimal(3), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6));
            await reader.CloseAsync();
            return response;
        }, ct);
    }

    public async Task<SessionResponse> RecordSessionCashCountAsync(TenantId tenant, Guid sessionId, CloseSessionRequest request, CancellationToken ct)
    {
        if (request.CountedCash < 0) throw new ArgumentException("pos.counted_cash_invalid");
        return await coordinator.ExecuteAsync<SessionResponse>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_session set counted_cash=$3,variance=$3-expected_cash,variance_reason=$4,row_version=row_version+1 where tenant_id=$1 and id=$2 and status='Reconciling' returning register_id,terminal_id,business_date,opening_float,status,counted_cash,variance", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(request.CountedCash); command.Parameters.AddWithValue((object?)request.VarianceReason ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.session_not_reconciling");
            var response = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(4), reader.GetDecimal(3), reader.GetDecimal(5), reader.GetDecimal(6));
            await reader.CloseAsync();
            return response;
        }, ct);
    }

    public async Task<SessionResponse> ApproveSessionVarianceAsync(TenantId tenant, Guid actor, Guid sessionId, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync<SessionResponse>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_session set status='Closed',approved_by=$3,row_version=row_version+1 where tenant_id=$1 and id=$2 and status='Reconciling' and variance is not null and variance_reason is not null and length(trim(variance_reason)) > 0 returning register_id,terminal_id,business_date,opening_float,status,counted_cash,variance", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(actor); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.session_variance_not_pending");
            var response = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(4), reader.GetDecimal(3), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)); await reader.CloseAsync();
            await NpgsqlAuditWriter.WriteAsync(ctx.Connection, ctx.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "Human", actor, null, "pos", "session_variance_approved", "pos_session", sessionId, null, JsonSerializer.Serialize(new { response.CountedCash, response.Variance, response.Status }), null, "pos-api", sessionId, System.Diagnostics.Activity.Current?.Id, DateTimeOffset.UtcNow, null), token);
            return response;
        }, ct);
    }

    public async Task<CartResponse> CreateCartAsync(TenantId tenant, Guid actor, CreateCartRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var session = new NpgsqlCommand("select s.status,r.currency_code from business_pos.pos_session s join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where s.tenant_id=$1 and s.id=$2", ctx.Connection, ctx.Transaction);
            session.Parameters.AddWithValue(tenant.Value); session.Parameters.AddWithValue(request.SessionId); await using var sessionReader = await session.ExecuteReaderAsync(token); if (!await sessionReader.ReadAsync(token)) throw new ArgumentException("pos.session_not_found"); var sessionStatus = sessionReader.GetString(0); var registerCurrency = sessionReader.GetString(1); await sessionReader.CloseAsync(); if (!string.Equals(sessionStatus, "Open", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.session_not_open"); if (!string.Equals(registerCurrency, request.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("pos.cart_currency_invalid");
            await using var command = new NpgsqlCommand("insert into business_pos.pos_cart(tenant_id,id,session_id,customer_party_id,currency_code,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$7)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.SessionId); command.Parameters.AddWithValue((object?)request.CustomerPartyId ?? DBNull.Value); command.Parameters.AddWithValue(request.CurrencyCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); return null;
        }, ct);
        return new(id, request.SessionId, "Active", 1, 0, 0, 0, 0);
    }

    public async Task<CartResponse> AddCartLineAsync(TenantId tenant, Guid cartId, AddCartLineRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0 || request.UnitPrice < 0) throw new ArgumentException("pos.cart_line_invalid");
        if (request.SerialId is not null && request.Quantity != 1m) throw new ArgumentException("pos.serial_quantity_invalid");
        var authoritativePrice = request.UnitPrice;
        var authoritativeTax = request.TaxAmount;
        string taxSnapshot;
        if (request.LegalEntityId is { } legalEntityId && request.ProductTemplateId is { } productTemplateId && request.UnitOfMeasureId is { } unitOfMeasureId)
        {
            var variant = await catalog.FindVariantAsync(tenant, request.VariantId, legalEntityId, ct);
            if (variant is null || !string.Equals(variant.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("pos.variant_not_sellable");
            var unitValidation = await quantityConversion.ValidateAsync(tenant, request.VariantId, unitOfMeasureId, request.Quantity, DateTimeOffset.UtcNow, legalEntityId, ct);
            if (!unitValidation.IsAllowed || !unitValidation.QuantityValid || !string.Equals(unitValidation.UnitCode, request.UomCode.Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("pos.uom_invalid");
            var price = await pricing.ResolveAsync(new PricingContext(tenant, new LegalEntityId(legalEntityId), PricingDirection.Selling, request.PricingDate ?? DateOnly.FromDateTime(DateTime.UtcNow), "EGP", request.PriceListId, null, null, request.VariantId, productTemplateId, Array.Empty<Guid>(), null, unitOfMeasureId, request.Quantity), ct);
            if (!string.Equals(price.Status, "Resolved", StringComparison.OrdinalIgnoreCase) || price.Snapshot is null || !decimal.TryParse(price.Snapshot.FinalUnitPrice, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out authoritativePrice)) throw new ArgumentException("pos.price_missing");
            var taxDate = request.PricingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var taxResult = await tax.CalculateAsync(new TaxContextContract(tenant, new LegalEntityId(legalEntityId), "Sales", "POS", "Sale", taxDate, taxDate, taxDate, taxDate, null, null, null, null, productTemplateId, request.VariantId, request.Quantity, unitOfMeasureId, "TaxExclusive", authoritativePrice.ToString(System.Globalization.CultureInfo.InvariantCulture), request.DiscountAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), "0", "EGP", "EGP", null), ct);
            if (!decimal.TryParse(taxResult.TaxTotal, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out authoritativeTax)) throw new ArgumentException("pos.tax_missing");
            taxSnapshot = JsonSerializer.Serialize(taxResult);
        }
        else throw new ArgumentException("pos.authoritative_context_required");
        var lineId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var expected = request.ExpectedVersion ?? 1;
        return await coordinator.ExecuteAsync<CartResponse>(tenant, async (ctx, token) =>
        {
            await using var cart = new NpgsqlCommand("select session_id,currency_code,version,state from business_pos.pos_cart where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction); cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(cartId);
            await using var reader = await cart.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found"); var sessionId = reader.GetGuid(0); var currency = reader.GetString(1); var version = reader.GetInt32(2); var state = reader.GetString(3); await reader.CloseAsync();
            if (state != "Active") throw new InvalidOperationException("pos.cart_not_editable"); if (version != expected) throw new InvalidOperationException("pos.cart_version_conflict");
            if (request.DiscountAmount > 0)
            {
                await using var overrideCheck = new NpgsqlCommand("delete from business_pos.pos_manager_override where tenant_id=$1 and scope='Discount' and target_id=$2 and expires_at > now() returning id", ctx.Connection, ctx.Transaction); overrideCheck.Parameters.AddWithValue(tenant.Value); overrideCheck.Parameters.AddWithValue(cartId); if (await overrideCheck.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.discount_override_required");
            }
            var lineTotal = PosTotals.LineTotal(request.Quantity, authoritativePrice, request.DiscountAmount, authoritativeTax);
            await using var line = new NpgsqlCommand("insert into business_pos.pos_cart_line(tenant_id,id,cart_id,line_no,variant_id,description,uom_code,unit_of_measure_id,quantity,unit_price,tax_amount,discount_amount,line_total,lot_id,serial_ids,price_snapshot,tax_snapshot,created_at) values($1,$2,$3,(select coalesce(max(line_no),0)+1 from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$3),$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15::jsonb,$16::jsonb,$17)", ctx.Connection, ctx.Transaction);
            line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(lineId); line.Parameters.AddWithValue(cartId); line.Parameters.AddWithValue(request.VariantId); line.Parameters.AddWithValue(request.Description.Trim()); line.Parameters.AddWithValue(request.UomCode.Trim()); line.Parameters.AddWithValue((object?)request.UnitOfMeasureId ?? DBNull.Value); line.Parameters.AddWithValue(request.Quantity); line.Parameters.AddWithValue(authoritativePrice); line.Parameters.AddWithValue(authoritativeTax); line.Parameters.AddWithValue(request.DiscountAmount); line.Parameters.AddWithValue(lineTotal); line.Parameters.AddWithValue((object?)request.LotId ?? DBNull.Value); line.Parameters.AddWithValue(request.SerialId is { } serial ? new[] { serial } : (object)DBNull.Value); line.Parameters.AddWithValue(JsonSerializer.Serialize(new { UnitPrice = authoritativePrice })); line.Parameters.AddWithValue(taxSnapshot); line.Parameters.AddWithValue(now); await line.ExecuteNonQueryAsync(token);
            await using var totals = new NpgsqlCommand("update business_pos.pos_cart as c set version=c.version+1,subtotal=x.subtotal,tax_total=x.tax_total,discount_total=x.discount_total,grand_total=x.grand_total,updated_at=$3 from (select coalesce(sum(quantity*unit_price),0) as subtotal,coalesce(sum(tax_amount),0) as tax_total,coalesce(sum(discount_amount),0) as discount_total,coalesce(sum(line_total),0) as grand_total from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2) as x where c.tenant_id=$1 and c.id=$2 returning c.version,c.subtotal,c.tax_total,c.discount_total,c.grand_total", ctx.Connection, ctx.Transaction); totals.Parameters.AddWithValue(tenant.Value); totals.Parameters.AddWithValue(cartId); totals.Parameters.AddWithValue(now); await using var updated = await totals.ExecuteReaderAsync(token); await updated.ReadAsync(token); var response = new CartResponse(cartId, sessionId, "Active", updated.GetInt32(0), updated.GetDecimal(1), updated.GetDecimal(2), updated.GetDecimal(3), updated.GetDecimal(4)); await updated.CloseAsync(); return response;
        }, ct);
    }

    public async Task<CartResponse> ChangeCartStateAsync(TenantId tenant, Guid cartId, string state, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var command = new NpgsqlCommand("update business_pos.pos_cart set state=$3,updated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and state in ('Active','Held')", ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); command.Parameters.AddWithValue(state); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.cart_state_conflict"); return null; }, ct);
        return await GetCartAsync(tenant, cartId, ct);
    }

    public async Task<CartResponse> UpdateCartLineAsync(TenantId tenant, Guid cartId, Guid lineId, UpdateCartLineRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0m || request.DiscountAmount < 0m || request.ExpectedVersion < 1) throw new ArgumentException("pos.cart_line_invalid");
        return await coordinator.ExecuteAsync<CartResponse>(tenant, async (ctx, token) =>
        {
            await using var cart = new NpgsqlCommand("select session_id,version,state from business_pos.pos_cart where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(cartId);
            await using var cartReader = await cart.ExecuteReaderAsync(token);
            if (!await cartReader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found");
            var sessionId = cartReader.GetGuid(0); var version = cartReader.GetInt32(1); var state = cartReader.GetString(2); await cartReader.CloseAsync();
            if (state != "Active") throw new InvalidOperationException("pos.cart_not_editable");
            if (version != request.ExpectedVersion) throw new InvalidOperationException("pos.cart_version_conflict");
            if (request.DiscountAmount > 0m)
            {
                await using var approval = new NpgsqlCommand("delete from business_pos.pos_manager_override where tenant_id=$1 and scope='Discount' and target_id=$2 and expires_at > now() returning id", ctx.Connection, ctx.Transaction);
                approval.Parameters.AddWithValue(tenant.Value); approval.Parameters.AddWithValue(cartId);
                if (await approval.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.discount_override_required");
            }
            await using var line = new NpgsqlCommand("select unit_price,tax_amount from business_pos.pos_cart_line where tenant_id=$1 and id=$2 and cart_id=$3", ctx.Connection, ctx.Transaction);
            line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(lineId); line.Parameters.AddWithValue(cartId);
            await using var lineReader = await line.ExecuteReaderAsync(token);
            if (!await lineReader.ReadAsync(token)) throw new ArgumentException("pos.cart_line_not_found");
            var unitPrice = lineReader.GetDecimal(0); var taxAmount = lineReader.GetDecimal(1); await lineReader.CloseAsync();
            var lineTotal = PosTotals.LineTotal(request.Quantity, unitPrice, request.DiscountAmount, taxAmount);
            await using var update = new NpgsqlCommand("update business_pos.pos_cart_line set quantity=$4,discount_amount=$5,line_total=$6 where tenant_id=$1 and id=$2 and cart_id=$3", ctx.Connection, ctx.Transaction);
            update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(lineId); update.Parameters.AddWithValue(cartId); update.Parameters.AddWithValue(request.Quantity); update.Parameters.AddWithValue(request.DiscountAmount); update.Parameters.AddWithValue(lineTotal); await update.ExecuteNonQueryAsync(token);
            return await RecalculateCartInContextAsync(ctx, tenant, cartId, sessionId, token);
        }, ct);
    }

    public async Task<CartResponse> RemoveCartLineAsync(TenantId tenant, Guid cartId, Guid lineId, int expectedVersion, CancellationToken ct)
    {
        if (expectedVersion < 1) throw new ArgumentException("pos.cart_version_invalid");
        return await coordinator.ExecuteAsync<CartResponse>(tenant, async (ctx, token) =>
        {
            await using var cart = new NpgsqlCommand("select session_id,version,state from business_pos.pos_cart where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(cartId); await using var reader = await cart.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found");
            var sessionId = reader.GetGuid(0); var version = reader.GetInt32(1); var state = reader.GetString(2); await reader.CloseAsync();
            if (state != "Active") throw new InvalidOperationException("pos.cart_not_editable");
            if (version != expectedVersion) throw new InvalidOperationException("pos.cart_version_conflict");
            await using var delete = new NpgsqlCommand("delete from business_pos.pos_cart_line where tenant_id=$1 and id=$2 and cart_id=$3", ctx.Connection, ctx.Transaction);
            delete.Parameters.AddWithValue(tenant.Value); delete.Parameters.AddWithValue(lineId); delete.Parameters.AddWithValue(cartId);
            if (await delete.ExecuteNonQueryAsync(token) != 1) throw new ArgumentException("pos.cart_line_not_found");
            return await RecalculateCartInContextAsync(ctx, tenant, cartId, sessionId, token);
        }, ct);
    }

    public async Task<CartResponse> ApplyCartLineDiscountAsync(TenantId tenant, Guid cartId, DiscountCartRequest request, CancellationToken ct)
    {
        if (request.Amount < 0m || request.ExpectedVersion < 1) throw new ArgumentException("pos.discount_invalid");
        return await coordinator.ExecuteAsync<CartResponse>(tenant, async (ctx, token) =>
        {
            await using var cart = new NpgsqlCommand("select session_id,version,state from business_pos.pos_cart where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(cartId); await using var cartReader = await cart.ExecuteReaderAsync(token);
            if (!await cartReader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found");
            var sessionId = cartReader.GetGuid(0); var version = cartReader.GetInt32(1); var state = cartReader.GetString(2); await cartReader.CloseAsync();
            if (state != "Active") throw new InvalidOperationException("pos.cart_not_editable");
            if (version != request.ExpectedVersion) throw new InvalidOperationException("pos.cart_version_conflict");
            if (request.Amount > 0m)
            {
                await using var approval = new NpgsqlCommand("delete from business_pos.pos_manager_override where tenant_id=$1 and scope='Discount' and target_id=$2 and expires_at > now() returning id", ctx.Connection, ctx.Transaction);
                approval.Parameters.AddWithValue(tenant.Value); approval.Parameters.AddWithValue(cartId); if (await approval.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.discount_override_required");
            }
            await using var line = new NpgsqlCommand("select quantity,unit_price,tax_amount from business_pos.pos_cart_line where tenant_id=$1 and id=$2 and cart_id=$3", ctx.Connection, ctx.Transaction);
            line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(request.LineId); line.Parameters.AddWithValue(cartId); await using var lineReader = await line.ExecuteReaderAsync(token);
            if (!await lineReader.ReadAsync(token)) throw new ArgumentException("pos.cart_line_not_found");
            var quantity = lineReader.GetDecimal(0); var unitPrice = lineReader.GetDecimal(1); var taxAmount = lineReader.GetDecimal(2); await lineReader.CloseAsync();
            var total = PosTotals.LineTotal(quantity, unitPrice, request.Amount, taxAmount);
            await using var update = new NpgsqlCommand("update business_pos.pos_cart_line set discount_amount=$4,line_total=$5 where tenant_id=$1 and id=$2 and cart_id=$3", ctx.Connection, ctx.Transaction);
            update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(request.LineId); update.Parameters.AddWithValue(cartId); update.Parameters.AddWithValue(request.Amount); update.Parameters.AddWithValue(total); await update.ExecuteNonQueryAsync(token);
            return await RecalculateCartInContextAsync(ctx, tenant, cartId, sessionId, token);
        }, ct);
    }

    public async Task<CartResponse> SetCartCustomerAsync(TenantId tenant, Guid cartId, SetCartCustomerRequest request, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync<CartResponse>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_cart set customer_party_id=$3,updated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and state='Active' returning session_id,version,subtotal,tax_total,discount_total,grand_total", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); command.Parameters.AddWithValue((object?)request.CustomerPartyId ?? DBNull.Value); command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.cart_not_editable");
            var response = new CartResponse(cartId, reader.GetGuid(0), "Active", reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5)); await reader.CloseAsync(); return response;
        }, ct);
    }

    public async Task<object> LookupCatalogAsync(TenantId tenant, string query, Guid? legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("pos.catalog_query_required");
        return await catalog.SearchAsync(tenant, query.Trim(), null, legalEntityId, Math.Clamp(page, 1, 100000), Math.Clamp(pageSize, 1, 100), ct);
    }

    public async Task<CatalogVariantSnapshot> LookupBarcodeAsync(TenantId tenant, string barcode, Guid legalEntityId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) throw new ArgumentException("pos.barcode_required");
        return await catalog.ResolveVariantAsync(tenant, barcode.Trim(), DateTimeOffset.UtcNow, legalEntityId, ct)
            ?? throw new ArgumentException("pos.barcode_not_found");
    }

    public async Task<CartResponse> ScanCartLineAsync(TenantId tenant, Guid cartId, ScanCartLineRequest request, CancellationToken ct)
    {
        var variant = await catalog.ResolveVariantAsync(tenant, request.SkuOrBarcode.Trim(), DateTimeOffset.UtcNow, request.LegalEntityId, ct)
            ?? throw new ArgumentException("pos.barcode_not_found");
        var uom = request.UomCode?.Trim() is { Length: > 0 } entered ? entered : variant.BaseUomCode;
        var uomId = string.Equals(uom, variant.BaseUomCode, StringComparison.OrdinalIgnoreCase) ? variant.BaseUomId : variant.EnteredUomId;
        if (uomId is null) throw new ArgumentException("pos.uom_invalid");
        return await AddCartLineAsync(tenant, cartId, new AddCartLineRequest(variant.ProductVariantId, variant.DisplayName, uom, request.Quantity, 0m, 0m, request.DiscountAmount, request.ExpectedVersion, request.LegalEntityId, variant.ProductTemplateId, uomId, request.PriceListId, DateOnly.FromDateTime(DateTime.UtcNow), request.LotId, request.SerialId), ct);
    }

    public async Task<CheckoutResponse> CheckoutAsync(TenantId tenant, Guid actor, Guid membership, CheckoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Tenders.Count == 0) throw new ArgumentException("pos.checkout_invalid");
        if (request.Tenders.Any(tender => tender.Amount <= 0 || (tender.Method is not ("Cash" or "Card" or "Credit"))))
            throw new ArgumentException("pos.tender_method_unsupported");
        var cardTenders = request.Tenders.Where(tender => string.Equals(tender.Method, "Card", StringComparison.OrdinalIgnoreCase)).ToArray();
        var cashTenders = request.Tenders.Where(tender => string.Equals(tender.Method, "Cash", StringComparison.OrdinalIgnoreCase)).ToArray();
        var creditTenders = request.Tenders.Where(tender => string.Equals(tender.Method, "Credit", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (creditTenders.Length > 1 || creditTenders.Any(tender => tender.PaymentAccountId is not null || tender.ProviderConfigId is not null))
            throw new ArgumentException("pos.credit_tender_invalid");
        if (cardTenders.Length > 1 || cardTenders.Any(tender => tender.ProviderConfigId is not { } || tender.PaymentAccountId is not null))
            throw new ArgumentException("pos.card_tender_invalid");
        if (cashTenders.Length > 0 && cashTenders.Any(tender => tender.PaymentAccountId is not { }))
            throw new ArgumentException("pos.cash_checkout_required");
        if (cashTenders.Length > 0 && cashTenders.Select(tender => tender.PaymentAccountId).Distinct().Count() != 1)
            throw new ArgumentException("pos.cash_tender_account_invalid");
        var checkoutId = Guid.CreateVersion7(); var saleId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var prior = await ReadExistingCheckoutAsync(tenant, request.IdempotencyKey, ct);
        if (prior is not null)
        {
            if (!string.Equals(prior.Value.PayloadHash, hash, StringComparison.Ordinal)) throw new InvalidOperationException("pos.idempotency_payload_conflict");
            return prior.Value.Response;
        }
        var context = await ReadCheckoutContextAsync(tenant, request.CartId, ct);
        if (context.State != "Active") throw new InvalidOperationException("pos.cart_not_checkoutable");
        var tenderTotal = request.Tenders.Sum(tender => tender.Amount);
        if (tenderTotal < context.Total) throw new ArgumentException("pos.tender_insufficient");
        if (creditTenders.Length > 0)
        {
            if (cardTenders.Length > 0 || cashTenders.Length > 0 || creditTenders[0].Amount != context.Total || context.PartyId is not { } creditParty)
                throw new ArgumentException("pos.credit_sale_requires_exact_customer_tender");
            await ValidateCreditSaleAsync(tenant, context.LegalEntityId, creditParty, context.Total, ct);
        }
        if (cardTenders.Length > 0 && cardTenders[0].Amount > context.Total) throw new ArgumentException("pos.card_over_tender_invalid");
        if (cardTenders.Length > 0 && cashTenders.Length > 0 && tenderTotal != context.Total) throw new ArgumentException("pos.mixed_tender_exact_total_required");
        var cardParty = context.PartyId ?? Guid.Empty;
        if (cardTenders.Length > 0 && cardParty == Guid.Empty) throw new ArgumentException("pos.card_customer_required");
        if (cardTenders.Length == 0 && cashTenders.Length == 0 && creditTenders.Length == 0) throw new ArgumentException("pos.checkout_tender_required");
        var initialState = PosCheckoutDraftPolicy.InitialState(creditTenders.Length > 0);
        var draft = await InitializeCheckoutDraftAsync(tenant, actor, request, context, checkoutId, saleId, hash, now, initialState, ct);
        if (!draft.Created) return draft.Response;
        var response = draft.Response;
        PosPaymentResult? cashPayment = null;
        PosProviderPaymentResult? providerPayment = null;
        if (cardTenders.Length == 0 && cashTenders.Length > 0 && context.WarehouseId is { } cashWarehouseId)
        {
            try { await ReserveInventoryAsync(tenant, actor, request.CartId, saleId, context, request.IdempotencyKey, cashWarehouseId, ct); }
            catch { return response; }
        }
        if (cardTenders.Length > 0)
        {
            providerPayment = await payments.BeginCardAsync(tenant.Value, actor, membership,
                new PosCardPaymentRequest(context.LegalEntityId, context.BranchId, cardParty, cardTenders[0].Amount, context.Currency,
                    cardTenders[0].ProviderConfigId!.Value, $"pos-checkout:{request.IdempotencyKey}", $"pos-card:{request.IdempotencyKey}"), checkoutId, ct);
            await UpdatePaymentReferencesAsync(tenant, checkoutId, providerPayment, null, ct);
            response = await GetCheckoutAsync(tenant, checkoutId, ct);
            if (cashTenders.Length > 0 && PosPaymentBoundaryPolicy.IsProviderSuccess(providerPayment.State))
            {
                cashPayment = await payments.CaptureCashAsync(tenant.Value, actor, membership,
                    new PosCashPaymentRequest(context.LegalEntityId, context.BranchId, context.PartyId, cashTenders.Sum(tender => tender.Amount), context.Currency,
                        cashTenders[0].PaymentAccountId!.Value, DateOnly.FromDateTime(now.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime),
                        $"pos-checkout:{request.IdempotencyKey}:cash", $"pos-checkout:{request.IdempotencyKey}:cash"), checkoutId, ct);
                if (!string.Equals(cashPayment.State, "Completed", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.payment_not_completed");
            }
        }
        else if (cashTenders.Length > 0)
        {
            var paymentAccountId = cashTenders[0].PaymentAccountId!.Value;
            var cashAmount = cashTenders.Sum(tender => tender.Amount);
            cashPayment = await payments.CaptureCashAsync(tenant.Value, actor, membership,
                new PosCashPaymentRequest(context.LegalEntityId, context.BranchId, context.PartyId, cashAmount, context.Currency,
                    paymentAccountId, DateOnly.FromDateTime(now.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime), $"pos-checkout:{request.IdempotencyKey}",
                    $"pos-checkout:{request.IdempotencyKey}"), checkoutId, ct);
            if (!string.Equals(cashPayment.State, "Completed", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.payment_not_completed");
        }
        if (cashPayment is not null)
            await UpdatePaymentReferencesAsync(tenant, checkoutId, null, cashPayment, ct);
        if (cashPayment is not null && providerPayment is null)
            await MarkPaymentCapturedAsync(tenant, checkoutId, saleId, null, cashPayment, ct);
        else if (providerPayment is not null && PosPaymentBoundaryPolicy.IsProviderSuccess(providerPayment.State))
            await MarkPaymentCapturedAsync(tenant, checkoutId, saleId, providerPayment, null, ct);
        if ((cashPayment is not null && providerPayment is null)
            || (providerPayment is not null && PosPaymentBoundaryPolicy.IsProviderSuccess(providerPayment.State)))
            response = await GetCheckoutAsync(tenant, checkoutId, ct);
        if (cashPayment is not null)
            await RecordCapturedCashSaleAsync(tenant, actor, checkoutId, ct);
        if (providerPayment is not null && !PosPaymentBoundaryPolicy.IsProviderSuccess(providerPayment.State)) return response;
        if (context.WarehouseId is { } warehouseId)
        {
            try { await IssueInventoryAsync(tenant, actor, request.CartId, saleId, context, request.IdempotencyKey, warehouseId, ct); }
            catch { return response; }
        }
        if (context.PartyId is not { }) return response;
        try
        {
            var invoice = await invoicing.CreateAndFinalizeFromSourceAsync(tenant, actor, membership,
                await BuildInvoiceSourceAsync(tenant, request.CartId, saleId, context, request.IdempotencyKey, ct), Guid.CreateVersion7(), ct);
            await MarkInvoiceCompletedAsync(tenant, checkoutId, saleId, invoice, context.WarehouseId is not null, ct);
            return response with { State = "Completed", InventoryState = context.WarehouseId is null ? "NotRequired" : "Completed", DocumentState = "Completed", AccountingState = "Completed" };
        }
        catch
        {
            return response;
        }
    }

    private async Task<(CheckoutResponse Response, bool Created)> InitializeCheckoutDraftAsync(TenantId tenant, Guid actor, CheckoutRequest request, CheckoutContext context,
        Guid checkoutId, Guid saleId, string payloadHash, DateTimeOffset now, (string CheckoutState, string PaymentState) initialState, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync(tenant, async (ctx, token) =>
        {
            await using var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", ctx.Connection, ctx.Transaction);
            lockCommand.Parameters.AddWithValue($"pos-checkout:{tenant.Value:D}:{request.IdempotencyKey}");
            await lockCommand.ExecuteScalarAsync(token);
            await using var existing = new NpgsqlCommand("select c.id,s.id,c.payload_hash,c.state,c.payment_state,c.inventory_state,c.document_state,c.accounting_state,c.total_amount,c.currency_code from business_pos.pos_checkout c left join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id where c.tenant_id=$1 and c.idempotency_key=$2", ctx.Connection, ctx.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.IdempotencyKey);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                if (!string.Equals(existingReader.GetString(2), payloadHash, StringComparison.Ordinal)) throw new InvalidOperationException("pos.idempotency_payload_conflict");
                var priorResponse = new CheckoutResponse(existingReader.GetGuid(0), existingReader.IsDBNull(1) ? null : existingReader.GetGuid(1), existingReader.GetString(3), existingReader.GetString(4), existingReader.GetString(5), existingReader.GetString(6), existingReader.GetString(7), existingReader.GetDecimal(8), existingReader.GetString(9));
                await existingReader.CloseAsync();
                return (priorResponse, false);
            }
            await existingReader.CloseAsync();
            await using var cart = new NpgsqlCommand("select c.session_id,c.currency_code,c.subtotal,c.tax_total,c.discount_total,c.grand_total,c.state,s.status from business_pos.pos_cart c join business_pos.pos_session s on s.tenant_id=c.tenant_id and s.id=c.session_id where c.tenant_id=$1 and c.id=$2 for update of c,s", ctx.Connection, ctx.Transaction);
            cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(request.CartId);
            await using var cartReader = await cart.ExecuteReaderAsync(token);
            if (!await cartReader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found");
            var currency = cartReader.GetString(1);
            var subtotal = cartReader.GetDecimal(2);
            var tax = cartReader.GetDecimal(3);
            var discount = cartReader.GetDecimal(4);
            var total = cartReader.GetDecimal(5);
            var cartState = cartReader.GetString(6);
            var sessionState = cartReader.GetString(7);
            await cartReader.CloseAsync();
            if (!string.Equals(cartState, "Active", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.cart_not_checkoutable");
            if (!string.Equals(sessionState, "Open", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.session_not_open");
            if (request.Tenders.Sum(x => x.Amount) < total) throw new ArgumentException("pos.tender_insufficient");

            await using var insert = new NpgsqlCommand("insert into business_pos.pos_checkout(tenant_id,id,cart_id,idempotency_key,payload_hash,state,commercial_state,payment_state,inventory_state,document_state,regulatory_state,accounting_state,total_amount,currency_code,provider_reference,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Pending',$7,'Pending','Pending','NotRequired','Pending',$8,$9,NULL,$10,$11,$11)", ctx.Connection, ctx.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(checkoutId); insert.Parameters.AddWithValue(request.CartId); insert.Parameters.AddWithValue(request.IdempotencyKey); insert.Parameters.AddWithValue(payloadHash); insert.Parameters.AddWithValue(initialState.CheckoutState); insert.Parameters.AddWithValue(initialState.PaymentState); insert.Parameters.AddWithValue(total); insert.Parameters.AddWithValue(currency); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now);
            await insert.ExecuteNonQueryAsync(token);

            for (var index = 0; index < request.Tenders.Count; index++)
            {
                var tender = request.Tenders[index];
                var tenderState = string.Equals(tender.Method, "Credit", StringComparison.OrdinalIgnoreCase) ? "Accepted" : "Pending";
                await using var tenderInsert = new NpgsqlCommand("insert into business_pos.pos_tender(tenant_id,id,checkout_id,sequence_no,method,requested_amount,captured_amount,change_amount,state,external_reference,provider_state,created_at,updated_at) values($1,$2,$3,$4,$5,$6,0,0,$7,NULL,NULL,$8,$8)", ctx.Connection, ctx.Transaction);
                tenderInsert.Parameters.AddWithValue(tenant.Value); tenderInsert.Parameters.AddWithValue(Guid.CreateVersion7()); tenderInsert.Parameters.AddWithValue(checkoutId); tenderInsert.Parameters.AddWithValue(index + 1); tenderInsert.Parameters.AddWithValue(tender.Method); tenderInsert.Parameters.AddWithValue(tender.Amount); tenderInsert.Parameters.AddWithValue(tenderState); tenderInsert.Parameters.AddWithValue(now);
                await tenderInsert.ExecuteNonQueryAsync(token);
            }

            await using var sale = new NpgsqlCommand("insert into business_pos.pos_sale(tenant_id,id,checkout_id,register_id,session_id,business_date,receipt_number,currency_code,subtotal,tax_total,discount_total,grand_total,payment_state,inventory_state,document_state,accounting_state,created_by,created_at) select $1,$2,$3,s.register_id,s.id,s.business_date,concat('POS-',to_char($9,'YYYYMMDDHH24MISSMS'),'-',right(replace($3::text,'-',''),8)),$4,$5,$6,$7,$8,$12,'Pending','Pending','Pending',$10,$9 from business_pos.pos_session s join business_pos.pos_cart c on c.tenant_id=s.tenant_id and c.session_id=s.id where s.tenant_id=$1 and c.id=$11", ctx.Connection, ctx.Transaction);
            sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(saleId); sale.Parameters.AddWithValue(checkoutId); sale.Parameters.AddWithValue(currency); sale.Parameters.AddWithValue(subtotal); sale.Parameters.AddWithValue(tax); sale.Parameters.AddWithValue(discount); sale.Parameters.AddWithValue(total); sale.Parameters.AddWithValue(now); sale.Parameters.AddWithValue(actor); sale.Parameters.AddWithValue(request.CartId); sale.Parameters.AddWithValue(initialState.PaymentState);
            await sale.ExecuteNonQueryAsync(token);

            await using (var sourceLines = new NpgsqlCommand("select line_no,variant_id,description,uom_code,unit_of_measure_id,quantity,unit_price,tax_amount,discount_amount,line_total,lot_id,serial_ids,price_snapshot,tax_snapshot from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", ctx.Connection, ctx.Transaction))
            {
                sourceLines.Parameters.AddWithValue(tenant.Value); sourceLines.Parameters.AddWithValue(request.CartId);
                await using var sourceReader = await sourceLines.ExecuteReaderAsync(token);
                var copies = new List<object[]>();
                while (await sourceReader.ReadAsync(token)) copies.Add([sourceReader.GetInt32(0), sourceReader.GetGuid(1), sourceReader.GetString(2), sourceReader.GetString(3), sourceReader.IsDBNull(4) ? DBNull.Value : sourceReader.GetGuid(4), sourceReader.GetDecimal(5), sourceReader.GetDecimal(6), sourceReader.GetDecimal(7), sourceReader.GetDecimal(8), sourceReader.GetDecimal(9), sourceReader.IsDBNull(10) ? DBNull.Value : sourceReader.GetGuid(10), sourceReader.IsDBNull(11) ? DBNull.Value : sourceReader.GetValue(11), sourceReader.GetString(12), sourceReader.GetString(13)]);
                await sourceReader.CloseAsync();
                foreach (var copy in copies)
                {
                    await using var lineCopy = new NpgsqlCommand("insert into business_pos.pos_sale_line(tenant_id,id,sale_id,line_no,variant_id,description,uom_code,unit_of_measure_id,quantity,unit_price,tax_amount,discount_amount,line_total,lot_id,serial_id,price_snapshot,tax_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,($15::uuid[])[1],$16::jsonb,$17::jsonb)", ctx.Connection, ctx.Transaction);
                    lineCopy.Parameters.AddWithValue(tenant.Value); lineCopy.Parameters.AddWithValue(Guid.CreateVersion7()); lineCopy.Parameters.AddWithValue(saleId); for (var i = 0; i < copy.Length; i++) lineCopy.Parameters.AddWithValue(copy[i] ?? DBNull.Value); await lineCopy.ExecuteNonQueryAsync(token);
                }
            }

            await using var closeCart = new NpgsqlCommand("update business_pos.pos_cart set state='CheckedOut',version=version+1,updated_at=$3 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            closeCart.Parameters.AddWithValue(tenant.Value); closeCart.Parameters.AddWithValue(request.CartId); closeCart.Parameters.AddWithValue(now); await closeCart.ExecuteNonQueryAsync(token);
            return (new CheckoutResponse(checkoutId, saleId, initialState.CheckoutState, initialState.PaymentState, "Pending", "Pending", "Pending", total, currency), true);
        }, ct);
    }

    private async Task UpdatePaymentReferencesAsync(TenantId tenant, Guid checkoutId, PosProviderPaymentResult? providerPayment, PosPaymentResult? cashPayment, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (providerPayment is not null)
            {
                await using var checkout = new NpgsqlCommand("update business_pos.pos_checkout set provider_reference=$3,payment_state=$4,updated_at=$5,row_version=row_version+1 where tenant_id=$1 and id=$2 and provider_reference is null", ctx.Connection, ctx.Transaction);
                checkout.Parameters.AddWithValue(tenant.Value); checkout.Parameters.AddWithValue(checkoutId); checkout.Parameters.AddWithValue(providerPayment.PaymentRequestId.ToString("D")); checkout.Parameters.AddWithValue(providerPayment.State); checkout.Parameters.AddWithValue(now); await checkout.ExecuteNonQueryAsync(token);
                await using var tender = new NpgsqlCommand("update business_pos.pos_tender set external_reference=$3,provider_state=$4,updated_at=$5 where tenant_id=$1 and checkout_id=$2 and method='Card' and state='Pending'", ctx.Connection, ctx.Transaction);
                tender.Parameters.AddWithValue(tenant.Value); tender.Parameters.AddWithValue(checkoutId); tender.Parameters.AddWithValue(providerPayment.PaymentRequestId.ToString("D")); tender.Parameters.AddWithValue(providerPayment.State); tender.Parameters.AddWithValue(now); await tender.ExecuteNonQueryAsync(token);
            }
            if (cashPayment is not null)
            {
                await using var tender = new NpgsqlCommand("update business_pos.pos_tender set external_reference=$3,updated_at=$4 where tenant_id=$1 and checkout_id=$2 and method='Cash' and state='Pending'", ctx.Connection, ctx.Transaction);
                tender.Parameters.AddWithValue(tenant.Value); tender.Parameters.AddWithValue(checkoutId); tender.Parameters.AddWithValue(cashPayment.PaymentId.ToString("D")); tender.Parameters.AddWithValue(now); await tender.ExecuteNonQueryAsync(token);
            }
            return null;
        }, ct);
    }

    public async Task<CheckoutResponse> GetCheckoutAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select c.id,s.id,c.state,c.payment_state,c.inventory_state,c.document_state,c.accounting_state,c.total_amount,c.currency_code from business_pos.pos_checkout c left join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id where c.tenant_id=$1 and c.id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(checkoutId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.checkout_not_found"); var result = new CheckoutResponse(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetString(8)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public Task<CheckoutResponse> RefreshPaymentStatusAsync(TenantId tenant, Guid checkoutId, CancellationToken ct) => RecoverCheckoutAsync(tenant, checkoutId, ct);

    public async Task<CheckoutResponse> AddCheckoutTenderAsync(TenantId tenant, Guid checkoutId, TenderRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m || !string.Equals(request.Method, "Cash", StringComparison.OrdinalIgnoreCase) || request.PaymentAccountId is null)
            throw new ArgumentException("pos.checkout_tender_invalid");
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var state = new NpgsqlCommand("select payment_state,total_amount from business_pos.pos_checkout where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction); state.Parameters.AddWithValue(tenant.Value); state.Parameters.AddWithValue(checkoutId); await using var reader = await state.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.checkout_not_found"); var paymentState = reader.GetString(0); var total = reader.GetDecimal(1); await reader.CloseAsync();
            if (paymentState is not ("Pending" or "RequiresAction" or "Failed")) throw new InvalidOperationException("pos.checkout_tenders_closed");
            await using var existing = new NpgsqlCommand("select coalesce(sum(requested_amount),0) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and state <> 'Failed'", ctx.Connection, ctx.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(checkoutId); var requested = (decimal)(await existing.ExecuteScalarAsync(token))!; if (requested + request.Amount > total) throw new ArgumentException("pos.tender_over_total");
            await using var sequence = new NpgsqlCommand("select coalesce(max(sequence_no),0)+1 from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2", ctx.Connection, ctx.Transaction); sequence.Parameters.AddWithValue(tenant.Value); sequence.Parameters.AddWithValue(checkoutId); var next = (int)(await sequence.ExecuteScalarAsync(token))!;
            var now = DateTimeOffset.UtcNow;
            await using var insert = new NpgsqlCommand("insert into business_pos.pos_tender(tenant_id,id,checkout_id,sequence_no,method,requested_amount,captured_amount,change_amount,state,external_reference,provider_state,created_at,updated_at) values($1,$2,$3,$4,'Cash',$5,0,0,'Pending',null,null,$6,$6)", ctx.Connection, ctx.Transaction); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(Guid.CreateVersion7()); insert.Parameters.AddWithValue(checkoutId); insert.Parameters.AddWithValue(next); insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
            if (string.Equals(paymentState, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                await using var reopen = new NpgsqlCommand("update business_pos.pos_checkout set state='PaymentPending',payment_state='Pending',recovery_reason=null,updated_at=$3,row_version=row_version+1 where tenant_id=$1 and id=$2 and state='PaymentFailed'", ctx.Connection, ctx.Transaction);
                reopen.Parameters.AddWithValue(tenant.Value); reopen.Parameters.AddWithValue(checkoutId); reopen.Parameters.AddWithValue(now); await reopen.ExecuteNonQueryAsync(token);
            }
            return null;
        }, ct);
        return await GetCheckoutAsync(tenant, checkoutId, ct);
    }

    private async Task ValidateCreditSaleAsync(TenantId tenant, Guid legalEntityId, Guid partyId, decimal amount, CancellationToken ct)
    {
        var profile = await subledger.GetFinancialProfileAsync(tenant, new LegalEntityId(legalEntityId), partyId, ct);
        var policy = string.IsNullOrWhiteSpace(profile?.ReceivablePolicyCode) ? "OFF" : profile.ReceivablePolicyCode.Trim().ToUpperInvariant();
        if (policy == "OFF") return;
        var balance = await subledger.GetPartyBalanceAsync(tenant, new PartyBalanceQuery(new LegalEntityId(legalEntityId), partyId, SubledgerDirectionContract.Receivable, DateOnly.FromDateTime(DateTime.UtcNow)), ct);
        var available = decimal.TryParse(balance.AvailableCreditAmount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : (decimal?)null;
        if (available is { } credit && amount > credit && policy is "BLOCK" or "REQUIREAPPROVAL" or "REQUIRE_APPROVAL")
            throw new InvalidOperationException("pos.credit_limit_exceeded");
    }

    public async Task<CheckoutResponse?> RecoverProviderCheckoutAsync(TenantId tenant, Guid paymentRequestId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id from business_pos.pos_checkout where tenant_id=$1 and provider_reference=$2 order by created_at desc limit 1", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(paymentRequestId.ToString("D")); var value = await command.ExecuteScalarAsync(ct); await tx.CommitAsync(ct);
        return value is Guid checkoutId ? await RecoverCheckoutAsync(tenant, checkoutId, ct) : null;
    }

    public async Task<CheckoutResponse> RecoverCheckoutAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        var pending = await ReadPendingCheckoutAsync(tenant, checkoutId, ct);
        if (pending is null || string.Equals(pending.State, "Completed", StringComparison.OrdinalIgnoreCase)) return await GetCheckoutAsync(tenant, checkoutId, ct);
        var checkoutContext = await ReadCheckoutContextAsync(tenant, pending.CartId, ct);
        if (!string.Equals(pending.PaymentState, "Captured", StringComparison.OrdinalIgnoreCase) && !string.Equals(pending.PaymentState, "NotRequired", StringComparison.OrdinalIgnoreCase))
        {
            var cashTotal = await ReadCashTenderTotalAsync(tenant, checkoutId, ct);
            PosProviderPaymentResult? provider = null;
            var failedProvider = await HasFailedCardTenderAsync(tenant, checkoutId, ct);
            if (cashTotal > 0m && checkoutContext.WarehouseId is { } cashWarehouseId && (pending.ProviderReference is null || failedProvider))
                await ReserveInventoryAsync(tenant, pending.ActorId, pending.CartId, pending.SaleId, checkoutContext, pending.IdempotencyKey, cashWarehouseId, ct);
            if (pending.ProviderReference is { } providerRequestId && !failedProvider)
            {
                provider = await payments.SyncCardAsync(tenant.Value, checkoutContext.LegalEntityId, providerRequestId, ct);
                if (!string.Equals(provider.State, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(provider.State, "Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(provider.State, "Cancelled", StringComparison.OrdinalIgnoreCase))
                        await MarkPaymentFailedAsync(tenant, checkoutId, pending.SaleId, provider.State, ct);
                    return await GetCheckoutAsync(tenant, checkoutId, ct);
                }
            }
            else if (cashTotal <= 0m)
            {
                throw new InvalidOperationException("pos.recovery_payment_not_captured");
            }
            if (failedProvider && cashTotal < checkoutContext.Total)
                throw new InvalidOperationException("pos.recovery_payment_not_captured");

            PosPaymentResult? recoveredCash = null;
            if (cashTotal > 0m)
            {
                var cashAccount = await ReadCashPaymentAccountAsync(tenant, checkoutContext.LegalEntityId, ct);
                recoveredCash = await payments.CaptureCashAsync(tenant.Value, pending.ActorId, await ReadMembershipAsync(tenant, pending.ActorId, ct),
                    new PosCashPaymentRequest(checkoutContext.LegalEntityId, checkoutContext.BranchId, checkoutContext.PartyId, cashTotal,
                        checkoutContext.Currency, cashAccount, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), checkoutId.ToString("D"),
                        $"pos-checkout:{pending.IdempotencyKey}:cash"), checkoutId, ct);
            }
            await MarkPaymentCapturedAsync(tenant, checkoutId, pending.SaleId, provider, recoveredCash, ct);
            if (cashTotal > 0m) await RecordCapturedCashSaleAsync(tenant, pending.ActorId, checkoutId, ct);
            pending = pending with { PaymentState = "Captured", State = "CompletionPending" };
        }
        var capturedCashTotal = await ReadCashTenderTotalAsync(tenant, checkoutId, ct);
        if (capturedCashTotal > 0m) await RecordCapturedCashSaleAsync(tenant, pending.ActorId, checkoutId, ct);
        if (checkoutContext.WarehouseId is { } warehouseId && !string.Equals(pending.InventoryState, "Completed", StringComparison.OrdinalIgnoreCase))
            await IssueInventoryAsync(tenant, pending.ActorId, pending.CartId, pending.SaleId, checkoutContext, pending.IdempotencyKey, warehouseId, ct);
        if (checkoutContext.PartyId is not { }) throw new InvalidOperationException("pos.recovery_customer_required");
        var membershipId = await ReadMembershipAsync(tenant, pending.ActorId, ct);
        var invoice = await ReadExistingInvoiceAsync(tenant, pending.ProviderReference, pending.SaleId, ct)
            ?? await invoicing.CreateAndFinalizeFromSourceAsync(tenant, pending.ActorId, membershipId,
                await BuildInvoiceSourceAsync(tenant, pending.CartId, pending.SaleId, checkoutContext, pending.IdempotencyKey, ct), pending.CheckoutId, ct);
        await MarkInvoiceCompletedAsync(tenant, pending.CheckoutId, pending.SaleId, invoice, checkoutContext.WarehouseId is not null, ct);
        return await GetCheckoutAsync(tenant, checkoutId, ct);
    }

    public async Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, CreateReturnRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0 || request.Lines.Select(x => x.OriginalSaleLineId).Distinct().Count() != request.Lines.Count || request.Lines.Any(x => x.Quantity <= 0 || x.Amount < 0) || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Disposition))
            throw new ArgumentException("pos.return_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        try
        {
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
            {
                await using var insert = new NpgsqlCommand("insert into business_pos.pos_return(tenant_id,id,original_sale_id,session_id,state,refund_state,disposition,reason,total_amount,created_by,created_at) values($1,$2,$3,$4,'Pending','NotRequested',$5,$6,$7,$8,$9)", ctx.Connection, ctx.Transaction);
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.OriginalSaleId); insert.Parameters.AddWithValue(request.SessionId); insert.Parameters.AddWithValue(request.Disposition.Trim()); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(request.Lines.Sum(x => x.Amount)); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
                foreach (var line in request.Lines)
                {
                    await using var candidate = new NpgsqlCommand("select sl.quantity,sl.unit_price,s.document_state,s.commercial_state from business_pos.pos_sale_line sl join business_pos.pos_sale s on s.tenant_id=sl.tenant_id and s.id=sl.sale_id where sl.tenant_id=$1 and sl.id=$2 and sl.sale_id=$3 and s.session_id=$4 for update", ctx.Connection, ctx.Transaction);
                    candidate.Parameters.AddWithValue(tenant.Value); candidate.Parameters.AddWithValue(line.OriginalSaleLineId); candidate.Parameters.AddWithValue(request.OriginalSaleId); candidate.Parameters.AddWithValue(request.SessionId);
                    await using var reader = await candidate.ExecuteReaderAsync(token);
                    if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.return_line_not_found");
                    var soldQuantity = reader.GetDecimal(0); var unitPrice = reader.GetDecimal(1); var documentState = reader.GetString(2); var commercialState = reader.GetString(3); await reader.CloseAsync();
                    if (!string.Equals(documentState, "Completed", StringComparison.OrdinalIgnoreCase) || !string.Equals(commercialState, "Completed", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.return_sale_not_completed");
                    await using var returned = new NpgsqlCommand("select coalesce(sum(quantity),0) from business_pos.pos_return_line rl join business_pos.pos_return r on r.tenant_id=rl.tenant_id and r.id=rl.return_id where rl.tenant_id=$1 and rl.original_sale_line_id=$2 and r.state in ('Pending','Completed')", ctx.Connection, ctx.Transaction);
                    returned.Parameters.AddWithValue(tenant.Value); returned.Parameters.AddWithValue(line.OriginalSaleLineId); var returnedQuantity = (decimal)(await returned.ExecuteScalarAsync(token) ?? 0m);
                    if (line.Quantity > soldQuantity - returnedQuantity) throw new InvalidOperationException("pos.return_quantity_exceeded");
                    if (line.Amount > unitPrice * line.Quantity + 0.01m) throw new InvalidOperationException("pos.return_amount_exceeded");
                    await using var lineInsert = new NpgsqlCommand("insert into business_pos.pos_return_line(tenant_id,id,return_id,original_sale_line_id,quantity,amount,disposition,lot_id,serial_ids) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", ctx.Connection, ctx.Transaction);
                    lineInsert.Parameters.AddWithValue(tenant.Value); lineInsert.Parameters.AddWithValue(Guid.CreateVersion7()); lineInsert.Parameters.AddWithValue(id); lineInsert.Parameters.AddWithValue(line.OriginalSaleLineId); lineInsert.Parameters.AddWithValue(line.Quantity); lineInsert.Parameters.AddWithValue(line.Amount); lineInsert.Parameters.AddWithValue(request.Disposition.Trim()); lineInsert.Parameters.AddWithValue((object?)line.LotId ?? DBNull.Value); lineInsert.Parameters.AddWithValue(line.SerialId is { } serial ? new[] { serial } : (object)DBNull.Value); await lineInsert.ExecuteNonQueryAsync(token);
                }
                return null;
            }, ct);
            await ProcessReturnAsync(tenant, actor, id, ct);
        }
        catch (PostgresException ex) when (ex.SqlState is "40P01" or "55P03" or "23505")
        {
            throw new PosConcurrencyException();
        }
        return id;
    }

    public Task<object> RecoverReturnAsync(TenantId tenant, Guid actor, Guid returnId, CancellationToken ct) => ProcessReturnAsync(tenant, actor, returnId, ct);

    public async Task<object> AddReturnLineAsync(TenantId tenant, Guid returnId, ReturnLineRequest line, CancellationToken ct)
    {
        if (line.Quantity <= 0 || line.Amount < 0) throw new ArgumentException("pos.return_line_invalid");
        return await coordinator.ExecuteAsync<object>(tenant, async (ctx, token) =>
        {
            await using var source = new NpgsqlCommand("select original_sale_id,session_id,disposition,state from business_pos.pos_return where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            source.Parameters.AddWithValue(tenant.Value); source.Parameters.AddWithValue(returnId);
            await using var sourceReader = await source.ExecuteReaderAsync(token);
            if (!await sourceReader.ReadAsync(token)) throw new ArgumentException("pos.return_not_found");
            var saleId = sourceReader.GetGuid(0); var sessionId = sourceReader.GetGuid(1); var disposition = sourceReader.GetString(2); var state = sourceReader.GetString(3); await sourceReader.CloseAsync();
            if (!string.Equals(state, "Pending", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.return_not_editable");
            await using var candidate = new NpgsqlCommand("select sl.quantity,sl.unit_price,s.document_state,s.commercial_state from business_pos.pos_sale_line sl join business_pos.pos_sale s on s.tenant_id=sl.tenant_id and s.id=sl.sale_id where sl.tenant_id=$1 and sl.id=$2 and sl.sale_id=$3 and s.session_id=$4", ctx.Connection, ctx.Transaction);
            candidate.Parameters.AddWithValue(tenant.Value); candidate.Parameters.AddWithValue(line.OriginalSaleLineId); candidate.Parameters.AddWithValue(saleId); candidate.Parameters.AddWithValue(sessionId);
            await using var candidateReader = await candidate.ExecuteReaderAsync(token);
            if (!await candidateReader.ReadAsync(token)) throw new ArgumentException("pos.return_line_not_found");
            var soldQuantity = candidateReader.GetDecimal(0); var unitPrice = candidateReader.GetDecimal(1); var documentState = candidateReader.GetString(2); var commercialState = candidateReader.GetString(3); await candidateReader.CloseAsync();
            if (!string.Equals(documentState, "Completed", StringComparison.OrdinalIgnoreCase) || !string.Equals(commercialState, "Completed", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.return_sale_not_completed");
            await using var returned = new NpgsqlCommand("select coalesce(sum(quantity),0) from business_pos.pos_return_line rl join business_pos.pos_return r on r.tenant_id=rl.tenant_id and r.id=rl.return_id where rl.tenant_id=$1 and rl.original_sale_line_id=$2 and r.state in ('Pending','Completed')", ctx.Connection, ctx.Transaction);
            returned.Parameters.AddWithValue(tenant.Value); returned.Parameters.AddWithValue(line.OriginalSaleLineId); var returnedQuantity = (decimal)(await returned.ExecuteScalarAsync(token) ?? 0m);
            if (line.Quantity > soldQuantity - returnedQuantity) throw new InvalidOperationException("pos.return_quantity_exceeded");
            if (line.Amount > unitPrice * line.Quantity + 0.01m) throw new InvalidOperationException("pos.return_amount_exceeded");
            await using var insert = new NpgsqlCommand("insert into business_pos.pos_return_line(tenant_id,id,return_id,original_sale_line_id,quantity,amount,disposition,lot_id,serial_ids) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", ctx.Connection, ctx.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(Guid.CreateVersion7()); insert.Parameters.AddWithValue(returnId); insert.Parameters.AddWithValue(line.OriginalSaleLineId); insert.Parameters.AddWithValue(line.Quantity); insert.Parameters.AddWithValue(line.Amount); insert.Parameters.AddWithValue(disposition); insert.Parameters.AddWithValue((object?)line.LotId ?? DBNull.Value); insert.Parameters.AddWithValue(line.SerialId is { } serial ? new[] { serial } : (object)DBNull.Value); await insert.ExecuteNonQueryAsync(token);
            return new { returnId, lineId = line.OriginalSaleLineId, state = "Pending" };
        }, ct);
    }

    public async Task<PosRefundResult> RefundReturnAsync(TenantId tenant, Guid actor, Guid membership, Guid returnId, RefundReturnRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.ReasonCode)) throw new ArgumentException("pos.refund_invalid");
        var captured = await coordinator.ExecuteAsync<(Guid LegalEntityId, Guid PaymentId, Guid PaymentRequestId, string Method, string Currency, decimal Total)>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("select r.refund_state,r.total_amount,s.currency_code,t.method,t.external_reference,pr.legal_entity_id from business_pos.pos_return r join business_pos.pos_sale s on s.tenant_id=r.tenant_id and s.id=r.original_sale_id join business_pos.pos_session psn on psn.tenant_id=s.tenant_id and psn.id=s.session_id join business_pos.pos_register pr on pr.tenant_id=psn.tenant_id and pr.id=psn.register_id join business_pos.pos_tender t on t.tenant_id=s.tenant_id and t.checkout_id=s.checkout_id where r.tenant_id=$1 and r.id=$2 and r.state='Completed' order by t.sequence_no limit 1 for update", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(returnId); await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.refund_return_not_found");
            var refundState = reader.GetString(0); var total = reader.GetDecimal(1); var currency = reader.GetString(2); var method = reader.GetString(3); var external = reader.IsDBNull(4) ? null : reader.GetString(4); var legalEntityId = reader.GetGuid(5); await reader.CloseAsync();
            if (!string.Equals(refundState, "NotRequested", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.refund_already_requested");
            if (!string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) && !string.Equals(method, "Card", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.refund_method_unsupported");
            if (!Guid.TryParse(external, out var paymentReference)) throw new InvalidOperationException("pos.refund_payment_reference_missing");
            if (request.Amount > total) throw new InvalidOperationException("pos.refund_amount_exceeded");
            await using var reserve = new NpgsqlCommand("update business_pos.pos_return set refund_state='Pending',refunded_amount=$3 where tenant_id=$1 and id=$2 and refund_state='NotRequested'", ctx.Connection, ctx.Transaction); reserve.Parameters.AddWithValue(tenant.Value); reserve.Parameters.AddWithValue(returnId); reserve.Parameters.AddWithValue(request.Amount); if (await reserve.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.refund_already_requested");
            return (legalEntityId, string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase) ? paymentReference : Guid.Empty, string.Equals(method, "Card", StringComparison.OrdinalIgnoreCase) ? paymentReference : Guid.Empty, method, currency, total);
        }, ct);
        try
        {
            var refundKey = $"pos-return-refund:{returnId:D}";
            var result = string.Equals(captured.Method, "Cash", StringComparison.OrdinalIgnoreCase)
                ? await payments.RefundCashAsync(tenant.Value, actor, membership, new PosCashRefundRequest(captured.LegalEntityId, captured.PaymentId, request.PaymentAccountId, request.Amount, captured.Currency, DateOnly.FromDateTime(DateTime.UtcNow), request.ReasonCode.Trim(), refundKey), returnId, ct)
                : await payments.RefundCardAsync(tenant.Value, actor, membership, captured.LegalEntityId,
                    (await payments.FindCapturedPaymentAsync(tenant.Value, captured.LegalEntityId, captured.PaymentRequestId, ct) ?? throw new InvalidOperationException("pos.provider_payment_not_found")).PaymentId,
                    request.PaymentAccountId, request.Amount, captured.Currency, DateOnly.FromDateTime(DateTime.UtcNow), request.ReasonCode.Trim(), refundKey, returnId, ct);
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var update = new NpgsqlCommand("update business_pos.pos_return set refund_state='Completed',refund_payment_id=$3 where tenant_id=$1 and id=$2 and refund_state='Pending'", ctx.Connection, ctx.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(returnId); update.Parameters.AddWithValue(result.RefundPaymentId); await update.ExecuteNonQueryAsync(token); return null; }, ct);
            return result;
        }
        catch
        {
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var update = new NpgsqlCommand("update business_pos.pos_return set refund_state='Failed',refunded_amount=0 where tenant_id=$1 and id=$2 and refund_state='Pending'", ctx.Connection, ctx.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(returnId); await update.ExecuteNonQueryAsync(token); return null; }, ct); throw;
        }
    }

    public async Task<PosInvoiceResult> CreateReturnCreditNoteAsync(TenantId tenant, Guid actor, Guid membership, Guid returnId, CancellationToken ct)
    {
        var source = await coordinator.ExecuteAsync<CreditNoteReturnSource>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("select r.credit_note_state,r.original_sale_id,r.reason,s.currency_code,pr.legal_entity_id,sl.line_no,sl.document_source_ref,rl.quantity from business_pos.pos_return r join business_pos.pos_return_line rl on rl.tenant_id=r.tenant_id and rl.return_id=r.id join business_pos.pos_sale_line sl on sl.tenant_id=rl.tenant_id and sl.id=rl.original_sale_line_id join business_pos.pos_sale s on s.tenant_id=sl.tenant_id and s.id=sl.sale_id join business_pos.pos_session psn on psn.tenant_id=s.tenant_id and psn.id=s.session_id join business_pos.pos_register pr on pr.tenant_id=psn.tenant_id and pr.id=psn.register_id where r.tenant_id=$1 and r.id=$2 and r.state='Completed' order by sl.line_no", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(returnId); await using var reader = await command.ExecuteReaderAsync(token); CreditNoteReturnSource? result = null;
            while (await reader.ReadAsync(token))
            {
                if (reader.IsDBNull(6)) throw new InvalidOperationException("pos.credit_note_invoice_missing");
                var invoiceId = reader.GetGuid(6);
                result ??= new(reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3).Trim(), reader.GetGuid(4), invoiceId, []);
                result = result with { Lines = [.. result.Lines, new(reader.GetInt32(5), reader.GetDecimal(7))] };
            }
            await reader.CloseAsync(); return result ?? throw new ArgumentException("pos.return_not_found");
        }, ct);
        if (!string.Equals(source.State, "NotRequested", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.credit_note_already_requested");
        var snapshot = await snapshots.GetInvoiceComplianceSnapshotAsync(tenant, source.LegalEntityId, source.InvoiceId, ct) ?? throw new InvalidOperationException("pos.credit_note_invoice_missing");
        var invoiceLines = snapshot.Lines.OrderBy(line => line.Sequence).ToArray();
        var correctionLines = source.Lines.Select(line => line.LineNo > 0 && line.LineNo <= invoiceLines.Length
            ? new InvoiceCreditNoteLineContract(invoiceLines[line.LineNo - 1].LineId, line.Quantity)
            : throw new InvalidOperationException("pos.credit_note_line_missing")).ToArray();
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var reserve = new NpgsqlCommand("update business_pos.pos_return set credit_note_state='Pending' where tenant_id=$1 and id=$2 and credit_note_state='NotRequested'", ctx.Connection, ctx.Transaction); reserve.Parameters.AddWithValue(tenant.Value); reserve.Parameters.AddWithValue(returnId); if (await reserve.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.credit_note_already_requested"); return null; }, ct);
        try
        {
            var result = await invoicing.CreateAndFinalizeCreditNoteAsync(tenant, actor, membership, new InvoiceCreditNoteRequest(source.InvoiceId, source.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), null, source.Reason, $"pos-credit-note:{returnId:D}", correctionLines), returnId, ct);
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var update = new NpgsqlCommand("update business_pos.pos_return set credit_note_state='Completed',credit_note_id=$3 where tenant_id=$1 and id=$2 and credit_note_state='Pending'", ctx.Connection, ctx.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(returnId); update.Parameters.AddWithValue(result.InvoiceId); await update.ExecuteNonQueryAsync(token); return null; }, ct);
            return result;
        }
        catch
        {
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var update = new NpgsqlCommand("update business_pos.pos_return set credit_note_state='Failed' where tenant_id=$1 and id=$2 and credit_note_state='Pending'", ctx.Connection, ctx.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(returnId); await update.ExecuteNonQueryAsync(token); return null; }, ct); throw;
        }
    }

    public async Task<ExchangeResponse> CreateExchangeAsync(TenantId tenant, Guid actor, Guid membership, ExchangeRequest request, CancellationToken ct)
    {
        var returnId = await CreateReturnAsync(tenant, actor, request.Return, ct);
        var exchangeId = Guid.CreateVersion7();
        await CreateExchangeGroupAsync(tenant, actor, exchangeId, returnId, ct);
        try
        {
            return await CompleteExchangeAsync(tenant, actor, membership, exchangeId, request.Checkout, ct);
        }
        catch
        {
            return new(exchangeId, returnId, null, null, "NeedsCompletion", 0m);
        }
    }

    public async Task<ExchangeResponse> CompleteExchangeAsync(TenantId tenant, Guid actor, Guid membership, Guid exchangeId, CheckoutRequest checkoutRequest, CancellationToken ct)
    {
        var existing = await ReadExchangeStateAsync(tenant, exchangeId, ct);
        if (existing is null) throw new ArgumentException("pos.exchange_not_found");
        if (string.Equals(existing.State, "Completed", StringComparison.OrdinalIgnoreCase) && existing.CheckoutId is { } completedCheckoutId)
        {
            var completed = await GetCheckoutAsync(tenant, completedCheckoutId, ct);
            return new(exchangeId, existing.ReturnId, completed.CheckoutId, completed.SaleId, "Completed", completed.Total - existing.ReturnTotal);
        }

        var returnScope = await ResolveResourceScopeAsync(tenant, "return", existing.ReturnId, ct)
            ?? throw new ArgumentException("pos.exchange_return_not_found");
        var replacementContext = await ReadCheckoutContextAsync(tenant, checkoutRequest.CartId, ct);
        if (replacementContext.BranchId is not { } replacementBranch
            || replacementContext.LegalEntityId != returnScope.LegalEntityId
            || replacementBranch != returnScope.BranchId)
            throw new InvalidOperationException("pos.exchange_scope_mismatch");

        CheckoutResponse checkout;
        try
        {
            checkout = await CheckoutAsync(tenant, actor, membership, checkoutRequest, ct);
        }
        catch
        {
            throw;
        }
        var state = string.Equals(checkout.State, "Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : "NeedsCompletion";
        var difference = checkout.Total - existing.ReturnTotal;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var update = new NpgsqlCommand("update business_pos.pos_exchange_group set new_checkout_id=$3,state=$4,difference_amount=$5 where tenant_id=$1 and id=$2 and state='NeedsCompletion'", ctx.Connection, ctx.Transaction);
            update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(exchangeId); update.Parameters.AddWithValue((object?)checkout.CheckoutId ?? DBNull.Value); update.Parameters.AddWithValue(state); update.Parameters.AddWithValue(difference); await update.ExecuteNonQueryAsync(token); return null;
        }, ct);
        return new(exchangeId, existing.ReturnId, checkout.CheckoutId, checkout.SaleId, state, difference);
    }

    private async Task CreateExchangeGroupAsync(TenantId tenant, Guid actor, Guid exchangeId, Guid returnId, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var insert = new NpgsqlCommand("insert into business_pos.pos_exchange_group(tenant_id,id,return_id,new_checkout_id,state,difference_amount,currency_code,created_by,created_at) values($1,$2,$3,NULL,'NeedsCompletion',0,coalesce((select s.currency_code from business_pos.pos_return r join business_pos.pos_sale s on s.tenant_id=r.tenant_id and s.id=r.original_sale_id where r.tenant_id=$1 and r.id=$3),'EGP'),$4,$5)", ctx.Connection, ctx.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(exchangeId); insert.Parameters.AddWithValue(returnId); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(DateTimeOffset.UtcNow); await insert.ExecuteNonQueryAsync(token); return null;
        }, ct);
    }

    private async Task<ExchangeState?> ReadExchangeStateAsync(TenantId tenant, Guid exchangeId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select g.return_id,g.new_checkout_id,g.state,g.difference_amount,r.total_amount from business_pos.pos_exchange_group g join business_pos.pos_return r on r.tenant_id=g.tenant_id and r.id=g.return_id where g.tenant_id=$1 and g.id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(exchangeId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await transaction.CommitAsync(ct); return null; }
        var state = new ExchangeState(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4)); await reader.CloseAsync(); await transaction.CommitAsync(ct); return state;
    }

    private sealed record ExchangeState(Guid ReturnId, Guid? CheckoutId, string State, decimal DifferenceAmount, decimal ReturnTotal);

    private sealed record CreditNoteReturnSource(string State, Guid SaleId, string Reason, string Currency, Guid LegalEntityId, Guid InvoiceId, IReadOnlyList<CreditNoteReturnLine> Lines);
    private sealed record CreditNoteReturnLine(int LineNo, decimal Quantity);

    private async Task<object> ProcessReturnAsync(TenantId tenant, Guid actor, Guid returnId, CancellationToken ct)
    {
        var lines = await coordinator.ExecuteAsync<List<ReturnInventoryLine>>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("select r.state,r.disposition,rl.original_sale_line_id,rl.quantity,sl.variant_id,sl.unit_of_measure_id,ps.legal_entity_id,ps.warehouse_id,ps.selling_location_id,coalesce(rl.lot_id,sl.lot_id),coalesce(rl.serial_ids[1],sl.serial_id) from business_pos.pos_return r join business_pos.pos_return_line rl on rl.tenant_id=r.tenant_id and rl.return_id=r.id join business_pos.pos_sale_line sl on sl.tenant_id=rl.tenant_id and sl.id=rl.original_sale_line_id join business_pos.pos_sale s on s.tenant_id=sl.tenant_id and s.id=sl.sale_id join business_pos.pos_session psn on psn.tenant_id=s.tenant_id and psn.id=s.session_id join business_pos.pos_register ps on ps.tenant_id=psn.tenant_id and ps.id=psn.register_id where r.tenant_id=$1 and r.id=$2", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(returnId); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ReturnInventoryLine>();
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetGuid(10)));
            await reader.CloseAsync(); return result;
        }, ct);
        if (lines.Count == 0) throw new ArgumentException("pos.return_not_found");
        if (string.Equals(lines[0].State, "Completed", StringComparison.OrdinalIgnoreCase)) return new { returnId, state = "Completed" };
        if (string.Equals(lines[0].Disposition, "Restock", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in lines)
            {
                if (line.UnitOfMeasureId is not { } unitId || line.WarehouseId is not { } warehouseId) throw new InvalidOperationException("pos.return_restock_context_missing");
                var returnDate = DateOnly.FromDateTime(DateTime.UtcNow);
                await inventory.ReceiveReturnAsync(tenant.Value, actor, new SalesInventoryReturnRequest(line.LegalEntityId, line.ProductVariantId, warehouseId, line.LocationId, unitId, line.Quantity, line.OriginalSaleLineId, line.OriginalSaleLineId, $"pos:return:{returnId:D}:line:{line.OriginalSaleLineId:D}", returnDate, returnDate, "Available", line.LotId, line.SerialId), ct);
            }
        }
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var update = new NpgsqlCommand("update business_pos.pos_return set state='Completed' where tenant_id=$1 and id=$2 and state='Pending'", ctx.Connection, ctx.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(returnId); await update.ExecuteNonQueryAsync(token); return null; }, ct);
        return new { returnId, state = "Completed" };
    }

    private sealed record ReturnInventoryLine(string State, string Disposition, Guid OriginalSaleLineId, decimal Quantity, Guid ProductVariantId, Guid? UnitOfMeasureId, Guid LegalEntityId, Guid? WarehouseId, Guid? LocationId, Guid? LotId, Guid? SerialId);

    public async Task<OfflinePackageResponse> DownloadOfflinePackageAsync(TenantId tenant, Guid registerId, string deviceIdentity, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceIdentity)) throw new ArgumentException("pos.offline_device_invalid");
        var signingSecret = Environment.GetEnvironmentVariable("TODIR_POS_OFFLINE_SIGNING_SECRET");
        if (string.IsNullOrWhiteSpace(signingSecret)) throw new InvalidOperationException("pos.offline_signing_unavailable");
        var now = DateTimeOffset.UtcNow;
        var validUntil = now.AddHours(24);
        return await coordinator.ExecuteAsync<OfflinePackageResponse>(tenant, async (ctx, token) =>
        {
            await using var details = new NpgsqlCommand("select r.legal_entity_id,r.branch_id,r.code,r.currency_code,r.offline_enabled,r.status,t.status,t.device_identity from business_pos.pos_register r join business_pos.pos_terminal t on t.tenant_id=r.tenant_id and t.register_id=r.id where r.tenant_id=$1 and r.id=$2 and t.device_identity=$3 for update", ctx.Connection, ctx.Transaction);
            details.Parameters.AddWithValue(tenant.Value); details.Parameters.AddWithValue(registerId); details.Parameters.AddWithValue(deviceIdentity.Trim());
            await using var reader = await details.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.offline_device_not_registered");
            if (!reader.GetBoolean(4) || !string.Equals(reader.GetString(5), "Active", StringComparison.OrdinalIgnoreCase) || !string.Equals(reader.GetString(6), "Ready", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("pos.offline_register_not_ready");
            var entityId = reader.GetGuid(0); var branchId = reader.GetGuid(1); var code = reader.GetString(2); var currency = reader.GetString(3); var actualDevice = reader.GetString(7);
            await reader.CloseAsync();
            await using var versionCommand = new NpgsqlCommand("select coalesce(max(version),0)+1 from business_pos.pos_offline_package where tenant_id=$1 and register_id=$2", ctx.Connection, ctx.Transaction);
            versionCommand.Parameters.AddWithValue(tenant.Value); versionCommand.Parameters.AddWithValue(registerId);
            var version = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
            var payload = JsonSerializer.Serialize(new { schemaVersion = 1, registerId, deviceIdentity = actualDevice, legalEntityId = entityId, branchId, registerCode = code, currencyCode = currency, downloadedAt = now, validUntil, capabilities = OfflineCapabilities });
            var signature = SignOfflinePayload(signingSecret, payload);
            var packageId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_pos.pos_offline_package(tenant_id,id,register_id,version,valid_from,expires_at,payload,signature,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8,$5)", ctx.Connection, ctx.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(packageId); insert.Parameters.AddWithValue(registerId); insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(now); insert.Parameters.AddWithValue(validUntil); insert.Parameters.AddWithValue(payload); insert.Parameters.AddWithValue(signature); await insert.ExecuteNonQueryAsync(token);
            return new(packageId, registerId, version, now, validUntil, payload, signature);
        }, ct);
    }

    public async Task<object> GetOfflineEnvelopeAsync(TenantId tenant, Guid envelopeId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,register_id,device_identity,source_sequence,idempotency_key,package_version,state,conflict_code,received_at,processed_at from business_pos.pos_offline_envelope where tenant_id=$1 and id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(envelopeId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.offline_envelope_not_found"); var result = new { id = reader.GetGuid(0), registerId = reader.GetGuid(1), deviceIdentity = reader.GetString(2), sourceSequence = reader.GetInt64(3), idempotencyKey = reader.GetString(4), packageVersion = reader.GetInt64(5), state = reader.GetString(6), conflictCode = reader.IsDBNull(7) ? null : reader.GetString(7), receivedAt = reader.GetFieldValue<DateTimeOffset>(8), processedAt = reader.IsDBNull(9) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(9) }; await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<object> ResolveOfflineConflictAsync(TenantId tenant, Guid actor, Guid conflictId, OfflineConflictResolutionRequest request, CancellationToken ct)
    {
        var decision = request.Decision?.Trim();
        if (decision is not ("AutoRecover" or "ManagerDecision" or "FinanceCorrection" or "RefundCustomer" or "SupportEscalation") || string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("pos.offline_conflict_resolution_invalid");
        return await coordinator.ExecuteAsync<object>(tenant, async (ctx, token) =>
        {
            await using var conflict = new NpgsqlCommand("select envelope_id,state from business_pos.pos_sync_conflict where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            conflict.Parameters.AddWithValue(tenant.Value); conflict.Parameters.AddWithValue(conflictId); await using var reader = await conflict.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.offline_conflict_not_found");
            var envelopeId = reader.GetGuid(0); var state = reader.GetString(1); await reader.CloseAsync();
            if (!string.Equals(state, "Open", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.offline_conflict_already_resolved");
            var now = DateTimeOffset.UtcNow;
            await using var update = new NpgsqlCommand("update business_pos.pos_sync_conflict set state='Resolved',resolved_by=$3,resolved_at=$4,details=details || jsonb_build_object('decision',$5::text,'note',$6::text) where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(conflictId); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(now); update.Parameters.AddWithValue(decision); update.Parameters.AddWithValue(request.Note.Trim()); await update.ExecuteNonQueryAsync(token);
            await using var envelope = new NpgsqlCommand("update business_pos.pos_offline_envelope set state=$3,processed_at=$4 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            envelope.Parameters.AddWithValue(tenant.Value); envelope.Parameters.AddWithValue(envelopeId); envelope.Parameters.AddWithValue(decision == "AutoRecover" ? "Accepted" : "NeedsReview"); envelope.Parameters.AddWithValue(now); await envelope.ExecuteNonQueryAsync(token);
            await NpgsqlAuditWriter.WriteAsync(ctx.Connection, ctx.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "Human", actor, null, "pos", "offline_conflict_resolved", "pos_sync_conflict", conflictId, null, JsonSerializer.Serialize(new { decision, note = request.Note.Trim(), envelopeId }), request.Note.Trim(), "pos-api", conflictId, System.Diagnostics.Activity.Current?.Id, now, null), token);
            ctx.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "PosOfflineConflictResolvedV1", JsonSerializer.Serialize(new { conflictId, envelopeId, decision }), tenant, now, conflictId, envelopeId, "pos", System.Diagnostics.Activity.Current?.Id));
            return new { conflictId, envelopeId, state = "Resolved", decision };
        }, ct);
    }

    public async Task<Guid> ReceiveOfflineEnvelopeAsync(TenantId tenant, OfflineEnvelopeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Signature) || string.IsNullOrWhiteSpace(request.IdempotencyKey) || string.IsNullOrWhiteSpace(request.DeviceIdentity) || request.SourceSequence <= 0 || request.PackageVersion <= 0)
            throw new ArgumentException("pos.offline_envelope_invalid");
        try { using var document = JsonDocument.Parse(request.Payload); }
        catch (JsonException) { throw new ArgumentException("pos.offline_payload_invalid"); }
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        var accepted = await coordinator.ExecuteAsync<OfflineEnvelopeAcceptance>(tenant, async (ctx, token) =>
        {
            await using var register = new NpgsqlCommand("select offline_enabled,status from business_pos.pos_register where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            register.Parameters.AddWithValue(tenant.Value); register.Parameters.AddWithValue(request.RegisterId); await using var registerReader = await register.ExecuteReaderAsync(token);
            if (!await registerReader.ReadAsync(token) || !registerReader.GetBoolean(0) || !string.Equals(registerReader.GetString(1), "Active", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.offline_register_not_enabled"); await registerReader.CloseAsync();
            await using var package = new NpgsqlCommand("select signature from business_pos.pos_offline_package where tenant_id=$1 and register_id=$2 and version=$3 and revoked_at is null and valid_from <= $4 and expires_at > $4", ctx.Connection, ctx.Transaction);
            package.Parameters.AddWithValue(tenant.Value); package.Parameters.AddWithValue(request.RegisterId); package.Parameters.AddWithValue(request.PackageVersion); package.Parameters.AddWithValue(now);
            var packageSignature = (string?)await package.ExecuteScalarAsync(token);
            if (string.IsNullOrWhiteSpace(packageSignature)) throw new InvalidOperationException("pos.offline_package_invalid");
            if (!PosOfflineSecurity.VerifyEnvelope(packageSignature, request.RegisterId, request.DeviceIdentity, request.SourceSequence, request.IdempotencyKey, request.PackageVersion, request.Payload, request.Signature))
                throw new InvalidOperationException("pos.offline_signature_invalid");
            await using var terminal = new NpgsqlCommand("select 1 from business_pos.pos_terminal where tenant_id=$1 and register_id=$2 and device_identity=$3 and status='Ready' limit 1", ctx.Connection, ctx.Transaction);
            terminal.Parameters.AddWithValue(tenant.Value); terminal.Parameters.AddWithValue(request.RegisterId); terminal.Parameters.AddWithValue(request.DeviceIdentity); if (await terminal.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.offline_device_not_registered");
            await using var existing = new NpgsqlCommand("select id from business_pos.pos_offline_envelope where tenant_id=$1 and idempotency_key=$2", ctx.Connection, ctx.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.IdempotencyKey); if (await existing.ExecuteScalarAsync(token) is Guid existingId) return new(existingId, false);
            await using var sequence = new NpgsqlCommand("select id from business_pos.pos_offline_envelope where tenant_id=$1 and register_id=$2 and device_identity=$3 and source_sequence=$4", ctx.Connection, ctx.Transaction);
            sequence.Parameters.AddWithValue(tenant.Value); sequence.Parameters.AddWithValue(request.RegisterId); sequence.Parameters.AddWithValue(request.DeviceIdentity); sequence.Parameters.AddWithValue(request.SourceSequence);
            if (await sequence.ExecuteScalarAsync(token) is Guid conflictingEnvelope)
            {
                var conflictId = Guid.CreateVersion7();
                await using var conflict = new NpgsqlCommand("insert into business_pos.pos_sync_conflict(tenant_id,id,envelope_id,code,details,created_at) values($1,$2,$3,'SourceSequenceConflict',$4::jsonb,$5)", ctx.Connection, ctx.Transaction);
                conflict.Parameters.AddWithValue(tenant.Value); conflict.Parameters.AddWithValue(conflictId); conflict.Parameters.AddWithValue(conflictingEnvelope); conflict.Parameters.AddWithValue(JsonSerializer.Serialize(new { request.DeviceIdentity, request.SourceSequence, request.IdempotencyKey })); conflict.Parameters.AddWithValue(now); await conflict.ExecuteNonQueryAsync(token);
                ctx.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "PosOfflineConflictV1", JsonSerializer.Serialize(new { conflictId, envelopeId = conflictingEnvelope, code = "SourceSequenceConflict" }), tenant, now, conflictId, conflictingEnvelope, "pos", System.Diagnostics.Activity.Current?.Id));
                return new OfflineEnvelopeAcceptance(Guid.Empty, true);
            }
            await using var command = new NpgsqlCommand("insert into business_pos.pos_offline_envelope(tenant_id,id,register_id,device_identity,source_sequence,idempotency_key,package_version,payload,signature,received_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.RegisterId); command.Parameters.AddWithValue(request.DeviceIdentity); command.Parameters.AddWithValue(request.SourceSequence); command.Parameters.AddWithValue(request.IdempotencyKey); command.Parameters.AddWithValue(request.PackageVersion); command.Parameters.AddWithValue(request.Payload); command.Parameters.AddWithValue(request.Signature); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); return new OfflineEnvelopeAcceptance(id, false);
        }, ct);
        if (accepted.Conflict) throw new InvalidOperationException("pos.offline_sequence_conflict");
        return accepted.EnvelopeId;
    }

    public async Task<OfflineCashSaleResponse> ProcessOfflineCashSaleAsync(TenantId tenant, Guid actor, Guid membership, Guid envelopeId, CancellationToken ct)
    {
        var envelope = await coordinator.ExecuteAsync<OfflineCashSaleEnvelope>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("select register_id,state,payload::text from business_pos.pos_offline_envelope where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(envelopeId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new ArgumentException("pos.offline_envelope_not_found");
            var registerId = reader.GetGuid(0); var state = reader.GetString(1); var payload = reader.GetString(2); await reader.CloseAsync();
            if (string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase))
                return new OfflineCashSaleEnvelope(registerId, state, payload);
            if (string.Equals(state, "Processing", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.offline_envelope_processing");
            if (!string.Equals(state, "Received", StringComparison.OrdinalIgnoreCase) && !string.Equals(state, "NeedsReview", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("pos.offline_envelope_not_processable");
            await using var update = new NpgsqlCommand("update business_pos.pos_offline_envelope set state='Processing',conflict_code=null,processed_at=null where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(envelopeId); await update.ExecuteNonQueryAsync(token);
            return new OfflineCashSaleEnvelope(registerId, "Processing", payload);
        }, ct);

        OfflineCashSalePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<OfflineCashSalePayload>(envelope.Payload, OfflineJsonOptions)
                ?? throw new ArgumentException("pos.offline_cash_sale_invalid");
            PosOfflineCashSalePolicy.Validate(payload);
        }
        catch
        {
            await MarkOfflineEnvelopeNeedsReviewAsync(tenant, envelopeId, "InvalidCashSalePayload", ct);
            throw;
        }

        if (string.Equals(envelope.State, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await ReadExistingCheckoutAsync(tenant, payload.IdempotencyKey, ct)
                ?? throw new InvalidOperationException("pos.offline_checkout_missing");
            return new(envelopeId, existing.Response, "Accepted");
        }

        try
        {
            await ValidateOfflineSessionAsync(tenant, envelope.RegisterId, payload.SessionId, ct);
            var existing = await ReadExistingCheckoutAsync(tenant, payload.IdempotencyKey, ct);
            CheckoutResponse checkout;
            if (existing is not null)
            {
                checkout = existing.Value.Response;
            }
            else
            {
                var cart = await CreateCartAsync(tenant, actor, new CreateCartRequest(payload.SessionId, payload.CurrencyCode, payload.CustomerPartyId), ct);
                foreach (var line in payload.Lines)
                {
                    await AddCartLineAsync(tenant, cart.Id, new AddCartLineRequest(line.VariantId, line.Description, line.UomCode, line.Quantity,
                        line.UnitPrice, line.TaxAmount, line.DiscountAmount, cart.Version, line.LegalEntityId, line.ProductTemplateId,
                        line.UnitOfMeasureId, line.PriceListId, line.PricingDate, line.LotId, line.SerialId), ct);
                    cart = await GetCartAsync(tenant, cart.Id, ct);
                }

                checkout = await CheckoutAsync(tenant, actor, membership, new CheckoutRequest(cart.Id, payload.IdempotencyKey,
                    [new TenderRequest("Cash", cart.GrandTotal, PaymentAccountId: payload.PaymentAccountId)]), ct);
            }

            if (!string.Equals(checkout.State, "Completed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("pos.offline_checkout_not_completed");
            await MarkOfflineEnvelopeAcceptedAsync(tenant, envelopeId, ct);
            return new(envelopeId, checkout, "Accepted");
        }
        catch
        {
            await MarkOfflineEnvelopeNeedsReviewAsync(tenant, envelopeId, "CashSaleProcessingFailed", ct);
            throw;
        }
    }

    private sealed record OfflineEnvelopeAcceptance(Guid EnvelopeId, bool Conflict);
    private sealed record OfflineCashSaleEnvelope(Guid RegisterId, string State, string Payload);

    public async Task<object> ListSalesAsync(TenantId tenant, Guid entityId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select s.id,s.receipt_number,s.business_date,s.currency_code,s.grand_total,s.payment_state,s.inventory_state,s.document_state from business_pos.pos_sale s join business_pos.pos_session ps on ps.tenant_id=s.tenant_id and ps.id=s.session_id where s.tenant_id=$1 and ps.register_id in (select id from business_pos.pos_register where tenant_id=$1 and legal_entity_id=$2) and ($3::date is null or s.business_date >= $3) and ($4::date is null or s.business_date <= $4) order by s.business_date desc,s.id desc offset $5 limit $6", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); command.Parameters.AddWithValue((page - 1) * pageSize); command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<object>(pageSize + 1); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new { id = reader.GetGuid(0), receiptNumber = reader.GetString(1), businessDate = reader.GetFieldValue<DateOnly>(2), currency = reader.GetString(3), total = reader.GetDecimal(4), paymentState = reader.GetString(5), inventoryState = reader.GetString(6), documentState = reader.GetString(7) });
        await reader.CloseAsync(); await tx.CommitAsync(ct);
        var hasMore = items.Count > pageSize; if (hasMore) items.RemoveAt(items.Count - 1);
        return new { page, pageSize, hasMore, items };
    }

    public async Task<object> ListReturnsAsync(TenantId tenant, Guid entityId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select r.id,r.original_sale_id,r.state,r.refund_state,r.disposition,r.reason,r.total_amount,r.created_at from business_pos.pos_return r join business_pos.pos_session ps on ps.tenant_id=r.tenant_id and ps.id=r.session_id join business_pos.pos_register pr on pr.tenant_id=ps.tenant_id and pr.id=ps.register_id where r.tenant_id=$1 and pr.legal_entity_id=$2 and ($3::date is null or r.created_at::date >= $3) and ($4::date is null or r.created_at::date <= $4) order by r.created_at desc,r.id desc offset $5 limit $6", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); command.Parameters.AddWithValue((page - 1) * pageSize); command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<object>(pageSize + 1); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new { id = reader.GetGuid(0), saleId = reader.GetGuid(1), state = reader.GetString(2), refundState = reader.GetString(3), disposition = reader.GetString(4), reason = reader.GetString(5), total = reader.GetDecimal(6), createdAt = reader.GetFieldValue<DateTimeOffset>(7) });
        await reader.CloseAsync(); await tx.CommitAsync(ct); var hasMore = items.Count > pageSize; if (hasMore) items.RemoveAt(items.Count - 1); return new { page, pageSize, hasMore, items };
    }

    public async Task<object> GetPaymentMixAsync(TenantId tenant, Guid entityId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select t.method,count(*),coalesce(sum(t.requested_amount),0),coalesce(sum(t.captured_amount),0),coalesce(sum(t.change_amount),0) from business_pos.pos_tender t join business_pos.pos_checkout c on c.tenant_id=t.tenant_id and c.id=t.checkout_id join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id join business_pos.pos_session ps on ps.tenant_id=s.tenant_id and ps.id=s.session_id join business_pos.pos_register pr on pr.tenant_id=ps.tenant_id and pr.id=ps.register_id where t.tenant_id=$1 and pr.legal_entity_id=$2 and t.state='Captured' and ($3::date is null or s.business_date >= $3) and ($4::date is null or s.business_date <= $4) group by t.method order by t.method", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value);
        var items = new List<object>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new { method = reader.GetString(0), tenderCount = reader.GetInt64(1), requested = reader.GetDecimal(2), captured = reader.GetDecimal(3), change = reader.GetDecimal(4) }); await reader.CloseAsync(); await tx.CommitAsync(ct); return new { from, to, items };
    }

    public async Task<object> GetDiscountSummaryAsync(TenantId tenant, Guid entityId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select coalesce(sum(s.discount_total),0),count(*) from business_pos.pos_sale s join business_pos.pos_session ps on ps.tenant_id=s.tenant_id and ps.id=s.session_id join business_pos.pos_register r on r.tenant_id=ps.tenant_id and r.id=ps.register_id where s.tenant_id=$1 and r.legal_entity_id=$2 and s.commercial_state='Completed' and ($3::date is null or s.business_date >= $3) and ($4::date is null or s.business_date <= $4)", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct); var result = new { from, to, discountTotal = reader.GetDecimal(0), saleCount = reader.GetInt64(1) }; await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<object> GetSessionSummaryAsync(TenantId tenant, Guid entityId, Guid sessionId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select ps.id,ps.business_date,ps.status,ps.opening_float,ps.expected_cash,ps.counted_cash,ps.variance,coalesce((select count(*) from business_pos.pos_sale s where s.tenant_id=ps.tenant_id and s.session_id=ps.id),0),coalesce((select sum(s.grand_total) from business_pos.pos_sale s where s.tenant_id=ps.tenant_id and s.session_id=ps.id),0),coalesce((select sum(t.captured_amount) from business_pos.pos_tender t join business_pos.pos_checkout c on c.tenant_id=t.tenant_id and c.id=t.checkout_id join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id where t.tenant_id=ps.tenant_id and s.session_id=ps.id and t.method='Cash' and t.state='Captured'),0) from business_pos.pos_session ps join business_pos.pos_register pr on pr.tenant_id=ps.tenant_id and pr.id=ps.register_id where ps.tenant_id=$1 and ps.id=$2 and pr.legal_entity_id=$3", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(entityId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.session_not_found"); var result = new { sessionId = reader.GetGuid(0), businessDate = reader.GetFieldValue<DateOnly>(1), status = reader.GetString(2), openingFloat = reader.GetDecimal(3), expectedCash = reader.GetDecimal(4), countedCash = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5), variance = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6), saleCount = reader.GetInt64(7), salesTotal = reader.GetDecimal(8), cashCaptured = reader.GetDecimal(9) }; await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<object> ListCashDifferencesAsync(TenantId tenant, Guid entityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 200); await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select ps.id,ps.business_date,ps.status,ps.expected_cash,ps.counted_cash,ps.variance,ps.variance_reason,ps.approved_by from business_pos.pos_session ps join business_pos.pos_register pr on pr.tenant_id=ps.tenant_id and pr.id=ps.register_id where ps.tenant_id=$1 and pr.legal_entity_id=$2 and ps.variance is not null order by ps.business_date desc,ps.id desc offset $3 limit $4", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue((page - 1) * pageSize); command.Parameters.AddWithValue(pageSize + 1); var items = new List<object>(pageSize + 1); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new { sessionId = reader.GetGuid(0), businessDate = reader.GetFieldValue<DateOnly>(1), status = reader.GetString(2), expectedCash = reader.GetDecimal(3), countedCash = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4), variance = reader.GetDecimal(5), reason = reader.GetString(6), approved = !reader.IsDBNull(7) }); await reader.CloseAsync(); await tx.CommitAsync(ct); var hasMore = items.Count > pageSize; if (hasMore) items.RemoveAt(items.Count - 1); return new { page, pageSize, hasMore, items };
    }

    public async Task<CartResponse> GetCartAsync(TenantId tenant, Guid cartId, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var cmd = new NpgsqlCommand("select session_id,state,version,subtotal,tax_total,discount_total,grand_total from business_pos.pos_cart where tenant_id=$1 and id=$2", connection, tx); cmd.Parameters.AddWithValue(tenant.Value); cmd.Parameters.AddWithValue(cartId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new ArgumentException("pos.cart_not_found"); var result = new CartResponse(cartId, r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6)); await r.CloseAsync(); await tx.CommitAsync(ct); return result; }

    public async Task<object> GetCartDetailsAsync(TenantId tenant, Guid cartId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct);
        await using var cart = new NpgsqlCommand("select session_id,state,version,subtotal,tax_total,discount_total,grand_total from business_pos.pos_cart where tenant_id=$1 and id=$2", connection, tx); cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(cartId);
        await using var cartReader = await cart.ExecuteReaderAsync(ct); if (!await cartReader.ReadAsync(ct)) throw new ArgumentException("pos.cart_not_found");
        var summary = new CartResponse(cartId, cartReader.GetGuid(0), cartReader.GetString(1), cartReader.GetInt32(2), cartReader.GetDecimal(3), cartReader.GetDecimal(4), cartReader.GetDecimal(5), cartReader.GetDecimal(6)); await cartReader.CloseAsync();
        await using var lines = new NpgsqlCommand("select id,line_no,variant_id,description,uom_code,quantity,unit_price,tax_amount,discount_amount,line_total from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", connection, tx); lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(cartId);
        var result = new List<CartLineResponse>(); await using var reader = await lines.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9))); await reader.CloseAsync(); await tx.CommitAsync(ct);
        return new { cart = summary, lines = result };
    }

    private static async Task<CartResponse> RecalculateCartInContextAsync(ITransactionContext ctx, TenantId tenant, Guid cartId, Guid sessionId, CancellationToken ct)
    {
        await using var totals = new NpgsqlCommand("update business_pos.pos_cart as c set version=c.version+1,subtotal=x.subtotal,tax_total=x.tax_total,discount_total=x.discount_total,grand_total=x.grand_total,updated_at=$3 from (select coalesce(sum(quantity*unit_price),0) as subtotal,coalesce(sum(tax_amount),0) as tax_total,coalesce(sum(discount_amount),0) as discount_total,coalesce(sum(line_total),0) as grand_total from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2) as x where c.tenant_id=$1 and c.id=$2 returning c.state,c.version,c.subtotal,c.tax_total,c.discount_total,c.grand_total", ctx.Connection, ctx.Transaction);
        totals.Parameters.AddWithValue(tenant.Value); totals.Parameters.AddWithValue(cartId); totals.Parameters.AddWithValue(DateTimeOffset.UtcNow); await using var reader = await totals.ExecuteReaderAsync(ct); await reader.ReadAsync(ct); var response = new CartResponse(cartId, sessionId, reader.GetString(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5)); await reader.CloseAsync(); return response;
    }

    public async Task<SessionResponse> GetSessionAsync(TenantId tenant, Guid sessionId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select register_id,terminal_id,business_date,status,opening_float,counted_cash,variance from business_pos.pos_session where tenant_id=$1 and id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.session_not_found"); var result = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<SessionResponse?> GetCurrentSessionAsync(TenantId tenant, Guid registerId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,terminal_id,business_date,status,opening_float,counted_cash,variance from business_pos.pos_session where tenant_id=$1 and register_id=$2 and status in ('Open','Reconciling') order by opened_at desc limit 1", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(registerId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await tx.CommitAsync(ct); return null; }
        var result = new SessionResponse(reader.GetGuid(0), registerId, reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }
    private static async Task AddCashMovementInContextAsync(ITransactionContext ctx, TenantId tenant, Guid sessionId, Guid actor, string type, decimal amount, string currency, string reason, CancellationToken ct, Guid? movementId = null, Guid? sourceReference = null)
    {
        await using var session = new NpgsqlCommand("select status from business_pos.pos_session where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction);
        session.Parameters.AddWithValue(tenant.Value); session.Parameters.AddWithValue(sessionId);
        var status = (string?)await session.ExecuteScalarAsync(ct);
        if (status is not ("Open" or "Reconciling")) throw new InvalidOperationException("pos.session_not_open");
        await using var command = new NpgsqlCommand("insert into business_pos.pos_cash_movement(tenant_id,id,session_id,movement_type,amount,currency_code,reason,source_ref,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", ctx.Connection, ctx.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(movementId ?? Guid.CreateVersion7()); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(type); command.Parameters.AddWithValue(amount); command.Parameters.AddWithValue(currency); command.Parameters.AddWithValue(reason); command.Parameters.AddWithValue((object?)sourceReference ?? DBNull.Value); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); await command.ExecuteNonQueryAsync(ct);
        var signed = type is "Refund" or "CashOut" or "Drop" ? -amount : amount;
        await using var expected = new NpgsqlCommand("update business_pos.pos_session set expected_cash=expected_cash+$3,row_version=row_version+1 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
        expected.Parameters.AddWithValue(tenant.Value); expected.Parameters.AddWithValue(sessionId); expected.Parameters.AddWithValue(signed); await expected.ExecuteNonQueryAsync(ct);
    }

    private async Task RecordCapturedCashSaleAsync(TenantId tenant, Guid actor, Guid checkoutId, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var existing = new NpgsqlCommand("select 1 from business_pos.pos_cash_movement where tenant_id=$1 and source_ref=$2 and movement_type='Sale' limit 1", ctx.Connection, ctx.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(checkoutId);
            if (await existing.ExecuteScalarAsync(token) is not null) return null;
            await using var details = new NpgsqlCommand("select s.id,s.currency_code,s.session_id from business_pos.pos_sale s join business_pos.pos_checkout c on c.tenant_id=s.tenant_id and c.id=s.checkout_id where s.tenant_id=$1 and s.checkout_id=$2", ctx.Connection, ctx.Transaction);
            details.Parameters.AddWithValue(tenant.Value); details.Parameters.AddWithValue(checkoutId);
            await using var reader = await details.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.sale_not_found");
            var sessionId = reader.GetGuid(2); var currency = reader.GetString(1); await reader.CloseAsync();
            await using var amount = new NpgsqlCommand("select coalesce(sum(requested_amount),0) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Cash'", ctx.Connection, ctx.Transaction);
            amount.Parameters.AddWithValue(tenant.Value); amount.Parameters.AddWithValue(checkoutId); var cash = (decimal)(await amount.ExecuteScalarAsync(token) ?? 0m);
            if (cash > 0) await AddCashMovementInContextAsync(ctx, tenant, sessionId, actor, "Sale", cash, currency, "POS sale", token, sourceReference: checkoutId);
            return null;
        }, ct);
    }

    private async Task ValidateOfflineSessionAsync(TenantId tenant, Guid registerId, Guid sessionId, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("select 1 from business_pos.pos_session where tenant_id=$1 and id=$2 and register_id=$3 and status='Open'", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(registerId);
            if (await command.ExecuteScalarAsync(token) is null) throw new InvalidOperationException("pos.offline_session_not_open");
            return null;
        }, ct);
    }

    private async Task MarkOfflineEnvelopeAcceptedAsync(TenantId tenant, Guid envelopeId, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_offline_envelope set state='Accepted',processed_at=$3 where tenant_id=$1 and id=$2 and state='Processing'", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(envelopeId); command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.offline_envelope_state_conflict");
            return null;
        }, ct);
    }

    private async Task MarkOfflineEnvelopeNeedsReviewAsync(TenantId tenant, Guid envelopeId, string code, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("update business_pos.pos_offline_envelope set state='NeedsReview',conflict_code=$3,processed_at=$4 where tenant_id=$1 and id=$2 and state='Processing'", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(envelopeId); command.Parameters.AddWithValue(code); command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(token);
            return null;
        }, ct);
    }

    private static async Task<NpgsqlTransaction> BeginTenantAsync(NpgsqlConnection connection, TenantId tenant, CancellationToken ct)
    { var tx = await connection.BeginTransactionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, tx); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return tx; }
    private static Task<string> PayloadHashAsync(CheckoutRequest request) => Task.FromResult(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)))));
    private static string SignOfflinePayload(string secret, string payload)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    private async Task<CheckoutContext> ReadCheckoutContextAsync(TenantId tenant, Guid cartId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select r.legal_entity_id,r.branch_id,r.warehouse_id,r.selling_location_id,c.customer_party_id,c.currency_code,c.grand_total,c.state from business_pos.pos_cart c join business_pos.pos_session s on s.tenant_id=c.tenant_id and s.id=c.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where c.tenant_id=$1 and c.id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.cart_not_found");
        var result = new CheckoutContext(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7)); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }
    private async Task<(CheckoutResponse Response, string PayloadHash)?> ReadExistingCheckoutAsync(TenantId tenant, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select c.id,s.id,c.payload_hash,c.state,c.payment_state,c.inventory_state,c.document_state,c.accounting_state,c.total_amount,c.currency_code from business_pos.pos_checkout c left join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id where c.tenant_id=$1 and c.idempotency_key=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(idempotencyKey); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await transaction.CommitAsync(ct); return null; }
        var response = new CheckoutResponse(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetDecimal(8), reader.GetString(9)); var payloadHash = reader.GetString(2); await reader.CloseAsync(); await transaction.CommitAsync(ct); return (response, payloadHash);
    }
    private sealed record CheckoutContext(Guid LegalEntityId, Guid? BranchId, Guid? WarehouseId, Guid? SellingLocationId, Guid? PartyId, string Currency, decimal Total, string State);
    private sealed record PendingCheckout(Guid CheckoutId, Guid CartId, Guid SaleId, Guid ActorId, string IdempotencyKey, string State, string PaymentState, string InventoryState, Guid? ProviderReference);

    private async Task<PendingCheckout?> ReadPendingCheckoutAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select c.id,c.cart_id,s.id,c.created_by,c.idempotency_key,c.state,c.payment_state,c.inventory_state,c.provider_reference from business_pos.pos_checkout c join business_pos.pos_sale s on s.tenant_id=c.tenant_id and s.checkout_id=c.id where c.tenant_id=$1 and c.id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(checkoutId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await transaction.CommitAsync(ct); return null; }
        var result = new PendingCheckout(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)));
        await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }

    private async Task<Guid> ReadMembershipAsync(TenantId tenant, Guid actorId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id from platform.membership where tenant_id=$1 and identity_id=$2 and status='Active' order by id limit 1", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(actorId); var value = await command.ExecuteScalarAsync(ct); await transaction.CommitAsync(ct);
        return value is Guid membership ? membership : throw new InvalidOperationException("pos.recovery_membership_missing");
    }

    private async Task<PosInvoiceResult?> ReadExistingInvoiceAsync(TenantId tenant, Guid? providerReference, Guid saleId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,document_number,journal_entry_id from business_invoicing.invoice where tenant_id=$1 and ((id=$2 and $2 is not null) or (source_module='pos' and source_document_id=$3)) and lifecycle_state='Finalized' order by finalized_at desc limit 1", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)providerReference ?? DBNull.Value); command.Parameters.AddWithValue(saleId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await transaction.CommitAsync(ct); return null; }
        var result = new PosInvoiceResult(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2)); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }
    private async Task<InvoiceSourceDocumentContract> BuildInvoiceSourceAsync(TenantId tenant, Guid cartId, Guid saleId, CheckoutContext context, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,variant_id,description,quantity,uom_code,unit_of_measure_id,unit_price from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); await using var reader = await command.ExecuteReaderAsync(ct); var lines = new List<InvoiceSourceLineContract>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetGuid(1), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetString(4), reader.GetString(2), reader.GetDecimal(3).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetDecimal(6).ToString(System.Globalization.CultureInfo.InvariantCulture), SourceLineReference: reader.GetGuid(0).ToString("D")));
        await reader.CloseAsync(); await transaction.CommitAsync(ct);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        return new("pos", saleId, "POSReceipt", $"pos-invoice:{idempotencyKey}", "Receivable", context.LegalEntityId, context.BranchId, context.PartyId!.Value, null, date, date, date, null, context.Currency, "Exclusive", null, null, null, lines);
    }
    private async Task IssueInventoryAsync(TenantId tenant, Guid actor, Guid cartId, Guid saleId, CheckoutContext context, string idempotencyKey, Guid warehouseId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,variant_id,unit_of_measure_id,quantity,lot_id,serial_ids from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); await using var reader = await command.ExecuteReaderAsync(ct); var lines = new List<InventoryLine>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<Guid[]>(5).FirstOrDefault()));
        await reader.CloseAsync(); await transaction.CommitAsync(ct);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var line in lines)
        {
            if (line.UnitOfMeasureId is not { } unitId) throw new InvalidOperationException("pos.inventory_uom_required");
            var issueKey = $"pos:{idempotencyKey}:issue:{line.Id:D}";
            if (await HasInventoryIssueAsync(tenant, saleId, issueKey, ct)) continue;
            var conversion = await quantityConversion.ConvertAsync(tenant, line.ProductVariantId, unitId, line.Quantity, DateTimeOffset.UtcNow, context.LegalEntityId, ct)
                ?? throw new InvalidOperationException("pos.inventory_uom_invalid");
            var reservation = await inventory.ReserveAsync(tenant.Value, actor, new SalesInventoryReservationRequest(context.LegalEntityId, line.ProductVariantId, warehouseId, line.Quantity, saleId, line.Id, $"pos:{idempotencyKey}:reserve:{line.Id:D}", LotId: line.LotId, SerialId: line.SerialId), ct);
            var issued = await inventory.IssueAsync(tenant.Value, actor, new SalesInventoryIssueRequest(context.LegalEntityId, reservation, line.ProductVariantId, unitId, warehouseId, conversion.Quantity, saleId, line.Id, issueKey, date, date, line.LotId, line.SerialId), ct);
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
            {
                await using var update = new NpgsqlCommand("update business_pos.pos_sale_line set inventory_source_ref=$3 where tenant_id=$1 and id=$2 and inventory_source_ref is null", ctx.Connection, ctx.Transaction);
                update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(line.Id); update.Parameters.AddWithValue(issued.OperationId); await update.ExecuteNonQueryAsync(token); return null;
            }, ct);
        }
    }

    private async Task ReserveInventoryAsync(TenantId tenant, Guid actor, Guid cartId, Guid saleId, CheckoutContext context, string idempotencyKey, Guid warehouseId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,variant_id,unit_of_measure_id,quantity,lot_id,serial_ids from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); await using var reader = await command.ExecuteReaderAsync(ct); var lines = new List<InventoryLine>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<Guid[]>(5).FirstOrDefault()));
        await reader.CloseAsync(); await transaction.CommitAsync(ct);
        var reserved = new List<Guid>();
        try
        {
            foreach (var line in lines)
            {
                if (line.UnitOfMeasureId is not { }) throw new InvalidOperationException("pos.inventory_uom_required");
                _ = await quantityConversion.ConvertAsync(tenant, line.ProductVariantId, line.UnitOfMeasureId.Value, line.Quantity, DateTimeOffset.UtcNow, context.LegalEntityId, ct)
                    ?? throw new InvalidOperationException("pos.inventory_uom_invalid");
                var reservation = await inventory.ReserveAsync(tenant.Value, actor,
                    new SalesInventoryReservationRequest(context.LegalEntityId, line.ProductVariantId, warehouseId, line.Quantity, saleId, line.Id,
                        $"pos:{idempotencyKey}:reserve:{line.Id:D}", null, line.LotId, line.SerialId), ct);
                reserved.Add(reservation);
            }
        }
        catch
        {
            foreach (var reservation in reserved)
            {
                try { await inventory.ReleaseAsync(tenant.Value, actor, context.LegalEntityId, reservation, ct); }
                catch { /* preserve the original inventory conflict for durable recovery */ }
            }
            throw;
        }
    }
    private async Task<bool> HasInventoryIssueAsync(TenantId tenant, Guid saleId, string issueKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select exists(select 1 from business_inventory.inventory_operation where tenant_id=$1 and source_module='sales' and source_document_id=$2 and operation_type='Issue' and status='Confirmed' and idempotency_key=$3)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(saleId); command.Parameters.AddWithValue(issueKey); var result = (bool)(await command.ExecuteScalarAsync(ct) ?? false); await transaction.CommitAsync(ct); return result;
    }
    private sealed record InventoryLine(Guid Id, Guid ProductVariantId, Guid? UnitOfMeasureId, decimal Quantity, Guid? LotId, Guid? SerialId);
    private async Task MarkInvoiceCompletedAsync(TenantId tenant, Guid checkoutId, Guid saleId, PosInvoiceResult invoice, bool inventoryCompleted, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            await using var checkout = new NpgsqlCommand("update business_pos.pos_checkout set state='Completed',commercial_state='Completed',inventory_state=$3,document_state='Completed',accounting_state='Completed',completed_at=$4,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and id=$2 and state='CompletionPending'", ctx.Connection, ctx.Transaction);
            checkout.Parameters.AddWithValue(tenant.Value); checkout.Parameters.AddWithValue(checkoutId); checkout.Parameters.AddWithValue(inventoryCompleted ? "Completed" : "NotRequired"); checkout.Parameters.AddWithValue(now); await checkout.ExecuteNonQueryAsync(token);
            await using var sale = new NpgsqlCommand("update business_pos.pos_sale set commercial_state='Completed',inventory_state=$3,document_state='Completed',accounting_state='Completed' where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(saleId); sale.Parameters.AddWithValue(inventoryCompleted ? "Completed" : "NotRequired"); await sale.ExecuteNonQueryAsync(token);
            await using var lines = new NpgsqlCommand("update business_pos.pos_sale_line set document_source_ref=$3 where tenant_id=$1 and sale_id=$2 and document_source_ref is null", ctx.Connection, ctx.Transaction);
            lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(saleId); lines.Parameters.AddWithValue(invoice.InvoiceId); await lines.ExecuteNonQueryAsync(token); return null;
        }, ct);
    }

    private async Task MarkPaymentCapturedAsync(TenantId tenant, Guid checkoutId, Guid saleId, PosProviderPaymentResult? providerPayment, PosPaymentResult? cashPayment, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            await using var checkout = new NpgsqlCommand("update business_pos.pos_checkout set state='CompletionPending',payment_state='Captured',updated_at=$3,row_version=row_version+1 where tenant_id=$1 and id=$2 and state='PaymentPending'", ctx.Connection, ctx.Transaction);
            checkout.Parameters.AddWithValue(tenant.Value); checkout.Parameters.AddWithValue(checkoutId); checkout.Parameters.AddWithValue(now); await checkout.ExecuteNonQueryAsync(token);
            await using var sale = new NpgsqlCommand("update business_pos.pos_sale set payment_state='Captured' where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(saleId); await sale.ExecuteNonQueryAsync(token);
            await using var tenders = new NpgsqlCommand("update business_pos.pos_tender set captured_amount=requested_amount,state='Captured',external_reference=case when method='Card' then coalesce($3,external_reference) when method='Cash' then coalesce($4,external_reference) else external_reference end,provider_state=case when method='Card' then coalesce($5,provider_state) else provider_state end,updated_at=$6 where tenant_id=$1 and checkout_id=$2 and state='Pending'", ctx.Connection, ctx.Transaction);
            tenders.Parameters.AddWithValue(tenant.Value); tenders.Parameters.AddWithValue(checkoutId); tenders.Parameters.AddWithValue((object?)providerPayment?.PaymentRequestId.ToString("D") ?? DBNull.Value); tenders.Parameters.AddWithValue((object?)cashPayment?.PaymentId.ToString("D") ?? DBNull.Value); tenders.Parameters.AddWithValue((object?)providerPayment?.State ?? DBNull.Value); tenders.Parameters.AddWithValue(now); await tenders.ExecuteNonQueryAsync(token);
            if (cashPayment is not null)
                ctx.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "PosCheckoutRecoveryRequiredV1", JsonSerializer.Serialize(new { checkoutId, saleId }), tenant, now, checkoutId, checkoutId, "pos", System.Diagnostics.Activity.Current?.Id));
            return null;
        }, ct);
    }

    private async Task MarkPaymentFailedAsync(TenantId tenant, Guid checkoutId, Guid saleId, string providerState, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            await using var checkout = new NpgsqlCommand("update business_pos.pos_checkout set state='PaymentFailed',payment_state=$3,recovery_reason='provider_failure',updated_at=$4,row_version=row_version+1 where tenant_id=$1 and id=$2 and state='PaymentPending'", ctx.Connection, ctx.Transaction);
            checkout.Parameters.AddWithValue(tenant.Value); checkout.Parameters.AddWithValue(checkoutId); checkout.Parameters.AddWithValue(providerState); checkout.Parameters.AddWithValue(now); await checkout.ExecuteNonQueryAsync(token);
            await using var sale = new NpgsqlCommand("update business_pos.pos_sale set payment_state=$3 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction);
            sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(saleId); sale.Parameters.AddWithValue(providerState); await sale.ExecuteNonQueryAsync(token);
            await using var tender = new NpgsqlCommand("update business_pos.pos_tender set state='Failed',provider_state=$3,updated_at=$4 where tenant_id=$1 and checkout_id=$2 and method='Card' and state='Pending'", ctx.Connection, ctx.Transaction);
            tender.Parameters.AddWithValue(tenant.Value); tender.Parameters.AddWithValue(checkoutId); tender.Parameters.AddWithValue(providerState); tender.Parameters.AddWithValue(now); await tender.ExecuteNonQueryAsync(token); return null;
        }, ct);
    }

    private async Task<decimal> ReadCashTenderTotalAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select coalesce(sum(requested_amount),0) from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Cash'", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(checkoutId); var result = (decimal)(await command.ExecuteScalarAsync(ct) ?? 0m); await transaction.CommitAsync(ct); return result;
    }

    private async Task<bool> HasFailedCardTenderAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select exists(select 1 from business_pos.pos_tender where tenant_id=$1 and checkout_id=$2 and method='Card' and state='Failed')", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(checkoutId); var result = (bool)(await command.ExecuteScalarAsync(ct) ?? false); await transaction.CommitAsync(ct); return result;
    }

    private async Task<Guid> ReadCashPaymentAccountAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select coalesce((select payment_account_id from finance_payments.payment_method_config where tenant_id=$1 and legal_entity_id=$2 and method='Cash' and status='Active' order by effective_from desc limit 1),(select id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and account_type='Cashbox' and status='Active' order by code limit 1))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); var result = await command.ExecuteScalarAsync(ct); await transaction.CommitAsync(ct); return result is Guid account ? account : throw new InvalidOperationException("pos.cash_account_missing");
    }
}
