using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Catalog;
using Shared.Contracts.Pricing;
using Shared.Contracts.Tax;
using Shared.Contracts.Payments;

namespace Modules.POS;

public sealed class PosService(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource, ICatalogCapabilities catalog, IPricingCapabilities pricing, ITaxCapabilities tax, IPosPaymentCapabilities payments)
{
    public async Task<RegisterResponse> CreateRegisterAsync(TenantId tenant, Guid actor, Guid entityId, CreateRegisterRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId != entityId || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("pos.register_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
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

    public async Task<TerminalResponse> RegisterTerminalAsync(TenantId tenant, Guid registerId, RegisterTerminalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceIdentity) || string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("pos.terminal_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_pos.pos_terminal(tenant_id,id,register_id,device_identity,display_name,status,created_at,updated_at) values($1,$2,$3,$4,$5,'Ready',$6,$6)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(registerId); command.Parameters.AddWithValue(request.DeviceIdentity.Trim()); command.Parameters.AddWithValue(request.DisplayName.Trim()); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); return null;
        }, ct);
        return new(id, registerId, request.DeviceIdentity.Trim(), request.DisplayName.Trim(), "Ready", 1, now);
    }

    public async Task<SessionResponse> OpenSessionAsync(TenantId tenant, Guid actor, Guid entityId, OpenSessionRequest request, CancellationToken ct)
    {
        if (request.OpeningFloat < 0) throw new ArgumentException("pos.opening_float_invalid");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        try
        {
            await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
            {
                await using var register = new NpgsqlCommand("select 1 from business_pos.pos_register where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status='Active'", ctx.Connection, ctx.Transaction); register.Parameters.AddWithValue(tenant.Value); register.Parameters.AddWithValue(request.RegisterId); register.Parameters.AddWithValue(entityId); if (await register.ExecuteScalarAsync(token) is null) throw new ArgumentException("pos.register_not_ready");
                await using var command = new NpgsqlCommand("insert into business_pos.pos_session(tenant_id,id,register_id,terminal_id,cashier_user_id,business_date,opened_at,status,opening_float,expected_cash,row_version) values($1,$2,$3,$4,$5,$6,$7,'Open',$8,$8,1)", ctx.Connection, ctx.Transaction);
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
            await using var command = new NpgsqlCommand("update business_pos.pos_session set status='Closed',closed_at=$4,counted_cash=$3,variance=$3-expected_cash,variance_reason=$5,row_version=row_version+1 where tenant_id=$1 and id=$2 and status='Open' returning register_id,terminal_id,business_date,opening_float,status,counted_cash,variance", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(request.CountedCash); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); command.Parameters.AddWithValue((object?)request.VarianceReason ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new InvalidOperationException("pos.session_not_open");
            var response = new SessionResponse(sessionId, reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(4), reader.GetDecimal(3), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6));
            await reader.CloseAsync();
            return response;
        }, ct);
    }

    public async Task<CartResponse> CreateCartAsync(TenantId tenant, Guid actor, CreateCartRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) =>
        {
            await using var command = new NpgsqlCommand("insert into business_pos.pos_cart(tenant_id,id,session_id,customer_party_id,currency_code,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$7)", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.SessionId); command.Parameters.AddWithValue((object?)request.CustomerPartyId ?? DBNull.Value); command.Parameters.AddWithValue(request.CurrencyCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); return null;
        }, ct);
        return new(id, request.SessionId, "Active", 1, 0, 0, 0, 0);
    }

    public async Task<CartResponse> AddCartLineAsync(TenantId tenant, Guid cartId, AddCartLineRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0 || request.UnitPrice < 0) throw new ArgumentException("pos.cart_line_invalid");
        var authoritativePrice = request.UnitPrice;
        var authoritativeTax = request.TaxAmount;
        string taxSnapshot;
        if (request.LegalEntityId is { } legalEntityId && request.ProductTemplateId is { } productTemplateId && request.UnitOfMeasureId is { } unitOfMeasureId)
        {
            var variant = await catalog.FindVariantAsync(tenant, request.VariantId, legalEntityId, ct);
            if (variant is null || !string.Equals(variant.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("pos.variant_not_sellable");
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
            var lineTotal = PosTotals.LineTotal(request.Quantity, authoritativePrice, request.DiscountAmount, request.TaxAmount);
            await using var line = new NpgsqlCommand("insert into business_pos.pos_cart_line(tenant_id,id,cart_id,line_no,variant_id,description,uom_code,quantity,unit_price,tax_amount,discount_amount,line_total,price_snapshot,tax_snapshot,created_at) values($1,$2,$3,(select coalesce(max(line_no),0)+1 from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$3),$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb,$13::jsonb,$14)", ctx.Connection, ctx.Transaction);
            line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(lineId); line.Parameters.AddWithValue(cartId); line.Parameters.AddWithValue(request.VariantId); line.Parameters.AddWithValue(request.Description.Trim()); line.Parameters.AddWithValue(request.UomCode.Trim()); line.Parameters.AddWithValue(request.Quantity); line.Parameters.AddWithValue(authoritativePrice); line.Parameters.AddWithValue(authoritativeTax); line.Parameters.AddWithValue(request.DiscountAmount); line.Parameters.AddWithValue(lineTotal); line.Parameters.AddWithValue(JsonSerializer.Serialize(new { UnitPrice = authoritativePrice })); line.Parameters.AddWithValue(taxSnapshot); line.Parameters.AddWithValue(now); await line.ExecuteNonQueryAsync(token);
            await using var totals = new NpgsqlCommand("update business_pos.pos_cart as c set version=c.version+1,subtotal=x.subtotal,tax_total=x.tax_total,discount_total=x.discount_total,grand_total=x.grand_total,updated_at=$3 from (select coalesce(sum(quantity*unit_price),0) as subtotal,coalesce(sum(tax_amount),0) as tax_total,coalesce(sum(discount_amount),0) as discount_total,coalesce(sum(line_total),0) as grand_total from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2) as x where c.tenant_id=$1 and c.id=$2 returning c.version,c.subtotal,c.tax_total,c.discount_total,c.grand_total", ctx.Connection, ctx.Transaction); totals.Parameters.AddWithValue(tenant.Value); totals.Parameters.AddWithValue(cartId); totals.Parameters.AddWithValue(now); await using var updated = await totals.ExecuteReaderAsync(token); await updated.ReadAsync(token); var response = new CartResponse(cartId, sessionId, "Active", updated.GetInt32(0), updated.GetDecimal(1), updated.GetDecimal(2), updated.GetDecimal(3), updated.GetDecimal(4)); await updated.CloseAsync(); return response;
        }, ct);
    }

    public async Task<CartResponse> ChangeCartStateAsync(TenantId tenant, Guid cartId, string state, CancellationToken ct)
    {
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var command = new NpgsqlCommand("update business_pos.pos_cart set state=$3,updated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and state in ('Active','Held')", ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); command.Parameters.AddWithValue(state); command.Parameters.AddWithValue(DateTimeOffset.UtcNow); if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("pos.cart_state_conflict"); return null; }, ct);
        return await GetCartAsync(tenant, cartId, ct);
    }

    public async Task<CheckoutResponse> CheckoutAsync(TenantId tenant, Guid actor, Guid membership, CheckoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Tenders.Count == 0) throw new ArgumentException("pos.checkout_invalid");
        if (request.Tenders.Count != 1 || !string.Equals(request.Tenders[0].Method, "Cash", StringComparison.OrdinalIgnoreCase) || request.Tenders[0].PaymentAccountId is not { } paymentAccountId)
            throw new ArgumentException("pos.cash_checkout_required");
        var checkoutId = Guid.CreateVersion7(); var saleId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        var prior = await ReadExistingCheckoutAsync(tenant, request.IdempotencyKey, ct);
        if (prior is not null)
        {
            if (!string.Equals(prior.Value.PayloadHash, hash, StringComparison.Ordinal)) throw new InvalidOperationException("pos.idempotency_payload_conflict");
            return prior.Value.Response;
        }
        var context = await ReadCheckoutContextAsync(tenant, request.CartId, ct);
        if (context.State != "Active") throw new InvalidOperationException("pos.cart_not_checkoutable");
        if (request.Tenders[0].Amount < context.Total) throw new ArgumentException("pos.tender_insufficient");
        var payment = await payments.CaptureCashAsync(tenant.Value, actor,
            membership, new PosCashPaymentRequest(context.LegalEntityId, context.BranchId, context.PartyId, context.Total, context.Currency,
                paymentAccountId, DateOnly.FromDateTime(now.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime), checkoutId.ToString("D"),
                $"pos-checkout:{request.IdempotencyKey}"), Guid.CreateVersion7(), ct);
        if (!string.Equals(payment.State, "Completed", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("pos.payment_not_completed");
        return await coordinator.ExecuteAsync(tenant, async (ctx, token) =>
        {
            await using var existing = new NpgsqlCommand("select id,total_amount,currency_code,state,payment_state,inventory_state,document_state,accounting_state from business_pos.pos_checkout where tenant_id=$1 and idempotency_key=$2", ctx.Connection, ctx.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.IdempotencyKey); await using var old = await existing.ExecuteReaderAsync(token); if (await old.ReadAsync(token)) { if (!string.Equals(hash, await PayloadHashAsync(request), StringComparison.Ordinal)) throw new InvalidOperationException("pos.idempotency_payload_conflict"); return new CheckoutResponse(old.GetGuid(0), null, old.GetString(3), old.GetString(4), old.GetString(5), old.GetString(6), old.GetString(7), old.GetDecimal(1), old.GetString(2)); } await old.CloseAsync();
            await using var cart = new NpgsqlCommand("select session_id,currency_code,subtotal,tax_total,discount_total,grand_total,state from business_pos.pos_cart where tenant_id=$1 and id=$2 for update", ctx.Connection, ctx.Transaction); cart.Parameters.AddWithValue(tenant.Value); cart.Parameters.AddWithValue(request.CartId); await using var cartReader = await cart.ExecuteReaderAsync(token); if (!await cartReader.ReadAsync(token)) throw new ArgumentException("pos.cart_not_found"); var sessionId = cartReader.GetGuid(0); var currency = cartReader.GetString(1); var subtotal = cartReader.GetDecimal(2); var tax = cartReader.GetDecimal(3); var discount = cartReader.GetDecimal(4); var total = cartReader.GetDecimal(5); var cartState = cartReader.GetString(6); await cartReader.CloseAsync(); if (cartState != "Active") throw new InvalidOperationException("pos.cart_not_checkoutable");
            var tenderTotal = request.Tenders.Sum(x => x.Amount); if (request.Tenders.Any(x => x.Amount <= 0) || tenderTotal < total) throw new ArgumentException("pos.tender_insufficient");
            await using var insert = new NpgsqlCommand("insert into business_pos.pos_checkout(tenant_id,id,cart_id,idempotency_key,payload_hash,state,commercial_state,payment_state,inventory_state,document_state,regulatory_state,accounting_state,total_amount,currency_code,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,'CompletionPending','Pending','Captured','Pending','Pending','NotRequired','Pending',$6,$7,$8,$9,$9)", ctx.Connection, ctx.Transaction); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(checkoutId); insert.Parameters.AddWithValue(request.CartId); insert.Parameters.AddWithValue(request.IdempotencyKey); insert.Parameters.AddWithValue(hash); insert.Parameters.AddWithValue(total); insert.Parameters.AddWithValue(currency); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
            for (var index = 0; index < request.Tenders.Count; index++) { var tender = request.Tenders[index]; await using var command = new NpgsqlCommand("insert into business_pos.pos_tender(tenant_id,id,checkout_id,sequence_no,method,requested_amount,captured_amount,change_amount,state,external_reference,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,case when $5='Cash' and $6>$8 then $6-$8 else 0 end,'Captured',$9,$10,$10)", ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(checkoutId); command.Parameters.AddWithValue(index + 1); command.Parameters.AddWithValue(tender.Method); command.Parameters.AddWithValue(tender.Amount); command.Parameters.AddWithValue(total); command.Parameters.AddWithValue(total); command.Parameters.AddWithValue(payment.PaymentId.ToString("D")); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(token); }
            await using var sale = new NpgsqlCommand("insert into business_pos.pos_sale(tenant_id,id,checkout_id,register_id,session_id,business_date,receipt_number,currency_code,subtotal,tax_total,discount_total,grand_total,payment_state,inventory_state,document_state,accounting_state,created_by,created_at) select $1,$2,$3,s.register_id,s.id,s.business_date,concat('POS-',to_char($9,'YYYYMMDDHH24MISSMS')), $4,$5,$6,$7,$8,'Captured','Pending','Pending','Pending',$10,$9 from business_pos.pos_session s join business_pos.pos_cart c on c.tenant_id=s.tenant_id and c.session_id=s.id where s.tenant_id=$1 and c.id=$11", ctx.Connection, ctx.Transaction); sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(saleId); sale.Parameters.AddWithValue(checkoutId); sale.Parameters.AddWithValue(currency); sale.Parameters.AddWithValue(subtotal); sale.Parameters.AddWithValue(tax); sale.Parameters.AddWithValue(discount); sale.Parameters.AddWithValue(total); sale.Parameters.AddWithValue(now); sale.Parameters.AddWithValue(actor); sale.Parameters.AddWithValue(request.CartId); await sale.ExecuteNonQueryAsync(token);
            await using (var sourceLines = new NpgsqlCommand("select line_no,variant_id,description,uom_code,quantity,unit_price,tax_amount,discount_amount,line_total,price_snapshot,tax_snapshot from business_pos.pos_cart_line where tenant_id=$1 and cart_id=$2 order by line_no", ctx.Connection, ctx.Transaction))
            {
                sourceLines.Parameters.AddWithValue(tenant.Value); sourceLines.Parameters.AddWithValue(request.CartId);
                await using var sourceReader = await sourceLines.ExecuteReaderAsync(token);
                var copies = new List<object[]>();
                while (await sourceReader.ReadAsync(token)) copies.Add([sourceReader.GetInt32(0), sourceReader.GetGuid(1), sourceReader.GetString(2), sourceReader.GetString(3), sourceReader.GetDecimal(4), sourceReader.GetDecimal(5), sourceReader.GetDecimal(6), sourceReader.GetDecimal(7), sourceReader.GetDecimal(8), sourceReader.GetString(9), sourceReader.GetString(10)]);
                await sourceReader.CloseAsync();
                foreach (var copy in copies)
                {
                    await using var lineCopy = new NpgsqlCommand("insert into business_pos.pos_sale_line(tenant_id,id,sale_id,line_no,variant_id,description,uom_code,quantity,unit_price,tax_amount,discount_amount,line_total,price_snapshot,tax_snapshot) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13::jsonb,$14::jsonb)", ctx.Connection, ctx.Transaction);
                    lineCopy.Parameters.AddWithValue(tenant.Value); lineCopy.Parameters.AddWithValue(Guid.CreateVersion7()); lineCopy.Parameters.AddWithValue(saleId); for (var i = 0; i < copy.Length; i++) lineCopy.Parameters.AddWithValue(copy[i] ?? DBNull.Value); await lineCopy.ExecuteNonQueryAsync(token);
                }
            }
            await using var closeCart = new NpgsqlCommand("update business_pos.pos_cart set state='CheckedOut',version=version+1,updated_at=$3 where tenant_id=$1 and id=$2", ctx.Connection, ctx.Transaction); closeCart.Parameters.AddWithValue(tenant.Value); closeCart.Parameters.AddWithValue(request.CartId); closeCart.Parameters.AddWithValue(now); await closeCart.ExecuteNonQueryAsync(token);
            ctx.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "PosCheckoutRecoveryRequiredV1", JsonSerializer.Serialize(new { checkoutId, saleId }), tenant, now, checkoutId, checkoutId, "pos", System.Diagnostics.Activity.Current?.Id));
            return new CheckoutResponse(checkoutId, saleId, "CompletionPending", "Captured", "Pending", "Pending", "Pending", total, currency);
        }, ct);
    }

    public async Task<CheckoutResponse> GetCheckoutAsync(TenantId tenant, Guid checkoutId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select id,state,payment_state,inventory_state,document_state,accounting_state,total_amount,currency_code from business_pos.pos_checkout where tenant_id=$1 and id=$2", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(checkoutId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.checkout_not_found"); var result = new CheckoutResponse(reader.GetGuid(0), null, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, CreateReturnRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0 || x.Amount < 0)) throw new ArgumentException("pos.return_invalid"); var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var insert = new NpgsqlCommand("insert into business_pos.pos_return(tenant_id,id,original_sale_id,session_id,state,refund_state,disposition,reason,total_amount,created_by,created_at) values($1,$2,$3,$4,'Pending','NotRequested',$5,$6,$7,$8,$9)", ctx.Connection, ctx.Transaction); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.OriginalSaleId); insert.Parameters.AddWithValue(request.SessionId); insert.Parameters.AddWithValue(request.Disposition); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(request.Lines.Sum(x => x.Amount)); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token); for (var i=0;i<request.Lines.Count;i++){ var line=request.Lines[i]; await using var lineInsert=new NpgsqlCommand("insert into business_pos.pos_return_line(tenant_id,id,return_id,original_sale_line_id,quantity,amount,disposition) values($1,$2,$3,$4,$5,$6,$7)",ctx.Connection,ctx.Transaction); lineInsert.Parameters.AddWithValue(tenant.Value);lineInsert.Parameters.AddWithValue(Guid.CreateVersion7());lineInsert.Parameters.AddWithValue(id);lineInsert.Parameters.AddWithValue(line.OriginalSaleLineId);lineInsert.Parameters.AddWithValue(line.Quantity);lineInsert.Parameters.AddWithValue(line.Amount);lineInsert.Parameters.AddWithValue(request.Disposition);await lineInsert.ExecuteNonQueryAsync(token);} return null; }, ct); return id;
    }

    public async Task<Guid> ReceiveOfflineEnvelopeAsync(TenantId tenant, OfflineEnvelopeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Signature) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ArgumentException("pos.offline_signature_required"); var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await coordinator.ExecuteAsync<object?>(tenant, async (ctx, token) => { await using var command = new NpgsqlCommand("insert into business_pos.pos_offline_envelope(tenant_id,id,register_id,device_identity,source_sequence,idempotency_key,package_version,payload,signature,received_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10) on conflict(tenant_id,idempotency_key) do nothing", ctx.Connection, ctx.Transaction); command.Parameters.AddWithValue(tenant.Value);command.Parameters.AddWithValue(id);command.Parameters.AddWithValue(request.RegisterId);command.Parameters.AddWithValue(request.DeviceIdentity);command.Parameters.AddWithValue(request.SourceSequence);command.Parameters.AddWithValue(request.IdempotencyKey);command.Parameters.AddWithValue(request.PackageVersion);command.Parameters.AddWithValue(request.Payload);command.Parameters.AddWithValue(request.Signature);command.Parameters.AddWithValue(now);await command.ExecuteNonQueryAsync(token); return null; }, ct); return id;
    }

    public async Task<object> ListSalesAsync(TenantId tenant, Guid entityId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 200); await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await BeginTenantAsync(connection, tenant, ct); await using var command = new NpgsqlCommand("select s.id,s.receipt_number,s.business_date,s.currency_code,s.grand_total,s.payment_state,s.inventory_state,s.document_state from business_pos.pos_sale s join business_pos.pos_session ps on ps.tenant_id=s.tenant_id and ps.id=s.session_id where s.tenant_id=$1 and ps.register_id in (select id from business_pos.pos_register where tenant_id=$1 and legal_entity_id=$2) and ($3::date is null or s.business_date >= $3) and ($4::date is null or s.business_date <= $4) order by s.business_date desc,s.id desc offset $5 limit $6", connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entityId);command.Parameters.AddWithValue((object?)from ?? DBNull.Value);command.Parameters.AddWithValue((object?)to ?? DBNull.Value);command.Parameters.AddWithValue((page-1)*pageSize);command.Parameters.AddWithValue(pageSize); var items=new List<object>(); await using var reader=await command.ExecuteReaderAsync(ct); while(await reader.ReadAsync(ct)) items.Add(new { id=reader.GetGuid(0), receiptNumber=reader.GetString(1), businessDate=reader.GetFieldValue<DateOnly>(2), currency=reader.GetString(3), total=reader.GetDecimal(4), paymentState=reader.GetString(5), inventoryState=reader.GetString(6), documentState=reader.GetString(7) }); await reader.CloseAsync(); await tx.CommitAsync(ct); return new { page, pageSize, items };
    }

    private async Task<CartResponse> GetCartAsync(TenantId tenant, Guid cartId, CancellationToken ct)
    { await using var connection=await dataSource.OpenConnectionAsync(ct); await using var tx=await BeginTenantAsync(connection,tenant,ct); await using var cmd=new NpgsqlCommand("select session_id,state,version,subtotal,tax_total,discount_total,grand_total from business_pos.pos_cart where tenant_id=$1 and id=$2",connection,tx);cmd.Parameters.AddWithValue(tenant.Value);cmd.Parameters.AddWithValue(cartId);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new ArgumentException("pos.cart_not_found");var result=new CartResponse(cartId,r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDecimal(6));await r.CloseAsync();await tx.CommitAsync(ct);return result; }
    private static async Task AddCashMovementInContextAsync(ITransactionContext ctx, TenantId tenant, Guid sessionId, Guid actor, string type, decimal amount, string currency, string reason, CancellationToken ct, Guid? movementId = null)
    { await using var command=new NpgsqlCommand("insert into business_pos.pos_cash_movement(tenant_id,id,session_id,movement_type,amount,currency_code,reason,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)",ctx.Connection,ctx.Transaction);command.Parameters.AddWithValue(tenant.Value);command.Parameters.AddWithValue(movementId ?? Guid.CreateVersion7());command.Parameters.AddWithValue(sessionId);command.Parameters.AddWithValue(type);command.Parameters.AddWithValue(amount);command.Parameters.AddWithValue(currency);command.Parameters.AddWithValue(reason);command.Parameters.AddWithValue(actor);command.Parameters.AddWithValue(DateTimeOffset.UtcNow);await command.ExecuteNonQueryAsync(ct); }
    private static async Task<NpgsqlTransaction> BeginTenantAsync(NpgsqlConnection connection, TenantId tenant, CancellationToken ct)
    { var tx=await connection.BeginTransactionAsync(ct);await using var command=new NpgsqlCommand("select set_config('app.tenant_id',$1,true)",connection,tx);command.Parameters.AddWithValue(tenant.Value.ToString("D"));await command.ExecuteNonQueryAsync(ct);return tx; }
    private static Task<string> PayloadHashAsync(CheckoutRequest request) => Task.FromResult(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)))));
    private async Task<CheckoutContext> ReadCheckoutContextAsync(TenantId tenant, Guid cartId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select r.legal_entity_id,r.branch_id,c.customer_party_id,c.currency_code,c.grand_total,c.state from business_pos.pos_cart c join business_pos.pos_session s on s.tenant_id=c.tenant_id and s.id=c.session_id join business_pos.pos_register r on r.tenant_id=s.tenant_id and r.id=s.register_id where c.tenant_id=$1 and c.id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(cartId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ArgumentException("pos.cart_not_found");
        var result = new CheckoutContext(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5)); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }
    private async Task<(CheckoutResponse Response, string PayloadHash)?> ReadExistingCheckoutAsync(TenantId tenant, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await BeginTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,payload_hash,state,payment_state,inventory_state,document_state,accounting_state,total_amount,currency_code from business_pos.pos_checkout where tenant_id=$1 and idempotency_key=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(idempotencyKey); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) { await reader.CloseAsync(); await transaction.CommitAsync(ct); return null; }
        var response = new CheckoutResponse(reader.GetGuid(0), null, reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetString(8)); var payloadHash = reader.GetString(1); await reader.CloseAsync(); await transaction.CommitAsync(ct); return (response, payloadHash);
    }
    private sealed record CheckoutContext(Guid LegalEntityId, Guid? BranchId, Guid? PartyId, string Currency, decimal Total, string State);
}
