using System.Text.Json;
using System.Globalization;
using Npgsql;
using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Core.Messaging;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;

namespace Modules.Directory.Repositories;

public sealed class DirectoryRepository(ITransactionCoordinator coordinator, NpgsqlDataSource dataSource)
{
    public Task<PartyDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreatePartyRequest request, Guid correlationId, string? traceParent, bool allowDuplicate, CancellationToken cancellationToken) => CreateAsync(tenantId, actorId, membershipId, request, correlationId, traceParent, allowDuplicate, null, cancellationToken);

    public async Task<PartyDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreatePartyRequest request, Guid correlationId, string? traceParent, bool allowDuplicate, string? idempotencyKey, CancellationToken cancellationToken)
    {
        Validate(request);
        var id = Guid.CreateVersion7();
        await coordinator.ExecuteAsync(tenantId, async (context, ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                await using var prior = new NpgsqlCommand("select response_json->>'partyId' from foundation_directory.import_operation where tenant_id=$1 and idempotency_key=$2", context.Connection, context.Transaction);
                Add(prior, tenantId.Value, idempotencyKey.Trim());
                if (await prior.ExecuteScalarAsync(ct) is string priorParty && Guid.TryParse(priorParty, out var priorId)) return priorId;
            }
            var normalizedName = DirectoryNormalization.Name(request.DisplayName);
            var duplicate = await FindDuplicateAsync(context.Connection, context.Transaction, tenantId, request, ct);
            if (duplicate is not null && !allowDuplicate) throw new DirectoryException("directory.duplicate_suspected", duplicate.Value.Reason);
            await ExecuteAsync(context, "insert into foundation_directory.party (tenant_id,id,party_type,status,display_name,display_name_normalized,legal_name,trade_name,name_ar,name_en,first_name,middle_name,last_name,country_code,primary_language,created_by,updated_by,created_at,updated_at,version) values ($1,$2,$3,'Active',$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$15,$16,$16,1)",
                ct, tenantId.Value, id, request.PartyType.ToString(), request.DisplayName.Trim(), normalizedName, Null(request.LegalName), Null(request.TradeName), Null(request.NameAr), Null(request.NameEn), Null(request.FirstName), Null(request.MiddleName), Null(request.LastName), Null(request.CountryCode), Null(request.PrimaryLanguage), actorId, now);
            foreach (var role in (request.Roles ?? []).Distinct()) await ExecuteAsync(context, "insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version,active_from) values ($1,$2,$3,'Active',$4,$5,$5,1,$5::date)", ct, tenantId.Value, id, role.ToString(), actorId, now);
            await ExecuteAsync(context, "insert into foundation_directory.party_visibility (tenant_id,visibility_id,party_id,scope_type) values ($1,$2,$3,'TenantWide')", ct, tenantId.Value, Guid.CreateVersion7(), id);
            foreach (var identifier in request.Identifiers ?? []) await InsertIdentifierAsync(context, tenantId, id, identifier, ct);
            foreach (var point in request.ContactPoints ?? []) await InsertContactPointAsync(context, tenantId, id, point, ct);
            foreach (var address in request.Addresses ?? []) await InsertAddressAsync(context, tenantId, id, address, ct);
            if (duplicate is not null) await ExecuteAsync(context, "insert into foundation_directory.duplicate_candidate (tenant_id,id,party_id,candidate_party_id,reason,confidence,status,created_at) values ($1,$2,$3,$4,$5,$6,'Open',$7)", ct, tenantId.Value, Guid.CreateVersion7(), id, duplicate.Value.PartyId, duplicate.Value.Reason, duplicate.Value.Confidence, now);
            await AuditAsync(context, tenantId, actorId, membershipId, id, "party_created", null, new { request.DisplayName, request.PartyType }, correlationId, now, ct);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyCreatedV1", JsonSerializer.Serialize(new { partyId = id }), tenantId, now, correlationId, id, "directory", traceParent));
            if (!string.IsNullOrWhiteSpace(idempotencyKey)) await ExecuteAsync(context, "insert into foundation_directory.import_operation (tenant_id,id,idempotency_key,operation_type,response_json,created_at,completed_at) values ($1,$2,$3,'DirectoryPartyCreate',jsonb_build_object('partyId',$4),$5,$5)", ct, tenantId.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), id, now);
            return id;
        }, cancellationToken);
        return await GetAsync(tenantId, id, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Created party could not be read.");
    }

    public async Task<PartyDetails> UpdateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, UpdatePartyRequest request, Guid correlationId, string? traceParent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new DirectoryException("directory.display_name_required", "Display name is required.");
        await coordinator.ExecuteAsync(tenantId, async (context, ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var before = await ReadIdentityAuditSnapshotAsync(context, tenantId, partyId, ct);
            var changed = await ExecuteAsync(context, "update foundation_directory.party set display_name=$3,display_name_normalized=$4,legal_name=$5,trade_name=$6,name_ar=$7,name_en=$8,first_name=$9,middle_name=$10,last_name=$11,country_code=$12,primary_language=$13,updated_by=$14,updated_at=$15,version=version+1 where tenant_id=$1 and id=$2 and version=$16", ct, tenantId.Value, partyId, request.DisplayName.Trim(), DirectoryNormalization.Name(request.DisplayName), Null(request.LegalName), Null(request.TradeName), Null(request.NameAr), Null(request.NameEn), Null(request.FirstName), Null(request.MiddleName), Null(request.LastName), Null(request.CountryCode), Null(request.PrimaryLanguage), actorId, now, request.Version);
            if (changed == 0) throw new DirectoryException("directory.concurrency_conflict", "The party was changed by another request.");
            var after = new { displayName = request.DisplayName.Trim(), legalName = Null(request.LegalName), tradeName = Null(request.TradeName), nameAr = Null(request.NameAr), nameEn = Null(request.NameEn), firstName = Null(request.FirstName), middleName = Null(request.MiddleName), lastName = Null(request.LastName), countryCode = Null(request.CountryCode), primaryLanguage = Null(request.PrimaryLanguage) };
            await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_identity_changed", before, after, correlationId, now, ct);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyIdentityChangedV1", JsonSerializer.Serialize(new { partyId }), tenantId, now, correlationId, partyId, "directory", traceParent));
            return true;
        }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, bool restore, long version, Guid correlationId, string? traceParent, CancellationToken cancellationToken)
    {
        var target = restore ? "Active" : "Archived";
        await coordinator.ExecuteAsync(tenantId, async (context, ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var changed = await ExecuteAsync(context, "update foundation_directory.party set status=$3,updated_by=$4,updated_at=$5,version=version+1 where tenant_id=$1 and id=$2 and version=$6 and status=$7", ct, tenantId.Value, partyId, target, actorId, now, version, restore ? "Archived" : "Active");
            if (changed == 0) throw new DirectoryException("directory.concurrency_conflict", "The party status was changed by another request or is not in the expected state.");
            await AuditAsync(context, tenantId, actorId, membershipId, partyId, restore ? "party_restored" : "party_archived", null, new { status = target }, correlationId, now, ct);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), target == PartyStatus.Archived.ToString() ? "DirectoryPartyArchivedV1" : "DirectoryPartyRestoredV1", JsonSerializer.Serialize(new { partyId, status = target }), tenantId, now, correlationId, partyId, "directory", traceParent));
            return true;
        }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> AddRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, AddRoleRequest request, Guid correlationId, string? traceParent, CancellationToken cancellationToken)
    {
        await coordinator.ExecuteAsync(tenantId, async (context, ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var changed = await ExecuteAsync(context, "update foundation_directory.party set updated_by=$3,updated_at=$4,version=version+1 where tenant_id=$1 and id=$2 and version=$5", ct, tenantId.Value, partyId, actorId, now, request.Version);
            if (changed == 0) throw new DirectoryException("directory.concurrency_conflict", "The party was changed by another request.");
            await ExecuteAsync(context, "insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version,active_from) values ($1,$2,$3,'Active',$4,$5,$5,1,$5::date) on conflict (tenant_id,party_id,role_code) do update set status='Active',updated_at=excluded.updated_at,version=foundation_directory.party_role.version+1", ct, tenantId.Value, partyId, request.Role.ToString(), actorId, now);
            await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_role_changed", null, new { role = request.Role, status = "Active" }, correlationId, now, ct);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyRoleChangedV1", JsonSerializer.Serialize(new { partyId, role = request.Role.ToString() }), tenantId, now, correlationId, partyId, "directory", traceParent));
            return true;
        }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> AddIdentifierAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, IdentifierInput input, Guid correlationId, CancellationToken cancellationToken)
    {
        await coordinator.ExecuteAsync(tenantId, async (context, ct) => { var now = DateTimeOffset.UtcNow; await InsertIdentifierAsync(context, tenantId, partyId, input, ct); await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_identifier_added", null, new { type = input.Type }, correlationId, now, ct); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyIdentifierChangedV1", JsonSerializer.Serialize(new { partyId, type = input.Type }), tenantId, now, correlationId, partyId, "directory")); return true; }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> AddContactPointAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, ContactPointInput input, Guid correlationId, CancellationToken cancellationToken)
    {
        await coordinator.ExecuteAsync(tenantId, async (context, ct) => { var now = DateTimeOffset.UtcNow; await InsertContactPointAsync(context, tenantId, partyId, input, ct); await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_contact_point_added", null, new { type = input.Type }, correlationId, now, ct); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyContactPointChangedV1", JsonSerializer.Serialize(new { partyId, type = input.Type }), tenantId, now, correlationId, partyId, "directory")); return true; }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> AddAddressAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, AddressInput input, Guid correlationId, CancellationToken cancellationToken)
    {
        await coordinator.ExecuteAsync(tenantId, async (context, ct) => { var now = DateTimeOffset.UtcNow; await InsertAddressAsync(context, tenantId, partyId, input, ct); await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_address_added", null, new { usage = input.Usage }, correlationId, now, ct); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "DirectoryPartyAddressChangedV1", JsonSerializer.Serialize(new { partyId, usage = input.Usage }), tenantId, now, correlationId, partyId, "directory")); return true; }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PartyDetails> DisableRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, PartyRoleCode role, Guid correlationId, CancellationToken cancellationToken)
    {
        await coordinator.ExecuteAsync(tenantId, async (context, ct) => { var now = DateTimeOffset.UtcNow; var changed = await ExecuteAsync(context, "update foundation_directory.party_role set status='Inactive',updated_at=$4,version=version+1 where tenant_id=$1 and party_id=$2 and role_code=$3 and status='Active'", ct, tenantId.Value, partyId, role.ToString(), now); if (changed == 0) throw new DirectoryException("directory.role_not_found", "Role was not active."); await AuditAsync(context, tenantId, actorId, membershipId, partyId, "party_role_changed", null, new { role, status = "Inactive" }, correlationId, now, ct); return true; }, cancellationToken);
        return await GetAsync(tenantId, partyId, true, cancellationToken) ?? throw new DirectoryException("directory.party_not_found", "Party was not found.");
    }

    public async Task<PagedParties> ListAsync(TenantId tenantId, string? search, PartyType? partyType, PartyRoleCode? role, PartyStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var normalized = DirectoryNormalization.Name(search);
        const string filter = "where p.tenant_id=$1 and ($2='' or p.display_name_normalized like '%' || $2 || '%' or exists (select 1 from foundation_directory.party_identifier i where i.tenant_id=p.tenant_id and i.party_id=p.id and i.normalized_value like '%' || $2 || '%') or exists (select 1 from foundation_directory.contact_point c where c.tenant_id=p.tenant_id and c.party_id=p.id and c.normalized_value like '%' || $2 || '%')) and ($3='' or p.party_type=$3) and ($4='' or exists (select 1 from foundation_directory.party_role r where r.tenant_id=p.tenant_id and r.party_id=p.id and r.role_code=$4 and r.status='Active')) and ($5='' or p.status=$5)";
        await using var count = new NpgsqlCommand("select count(*) from foundation_directory.party p " + filter, connection, transaction);
        Add(count, tenantId.Value, normalized, partyType?.ToString() ?? "", role?.ToString() ?? "", status?.ToString() ?? "");
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select p.id,p.party_type,p.status,p.display_name,p.legal_name,p.updated_at,p.version,coalesce(array_agg(r.role_code) filter (where r.status='Active'),'{}') from foundation_directory.party p left join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id " + filter + " group by p.id,p.party_type,p.status,p.display_name,p.legal_name,p.updated_at,p.version order by p.updated_at desc,p.id limit $6 offset $7", connection, transaction);
        Add(command, tenantId.Value, normalized, partyType?.ToString() ?? "", role?.ToString() ?? "", status?.ToString() ?? "", pageSize, (page - 1) * pageSize);
        var items = new List<PartySummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(new PartySummary(reader.GetGuid(0), Enum.Parse<PartyType>(reader.GetString(1)), Enum.Parse<PartyStatus>(reader.GetString(2)), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<string[]>(7).Select(code => new PartyRoleDto(code, "Active", 1)).ToArray(), reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt64(6)));
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(items, total, page, pageSize);
    }

    public async Task<PartyDetails?> GetAsync(TenantId tenantId, Guid partyId, bool includeIdentifiers, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id,party_type,status,display_name,legal_name,trade_name,name_ar,name_en,first_name,middle_name,last_name,country_code,primary_language,created_at,updated_at,version from foundation_directory.party where tenant_id=$1 and id=$2", connection, transaction);
        Add(command, tenantId.Value, partyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.CloseAsync();
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        var details = new PartyDetails(reader.GetGuid(0), Enum.Parse<PartyType>(reader.GetString(1)), Enum.Parse<PartyStatus>(reader.GetString(2)), reader.GetString(3), Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8), Text(reader, 9), Text(reader, 10), Text(reader, 11), Text(reader, 12), [], [], [], [], reader.GetFieldValue<DateTimeOffset>(13), reader.GetFieldValue<DateTimeOffset>(14), reader.GetInt64(15));
        await reader.CloseAsync();
        var roles = await ReadRolesAsync(connection, transaction, tenantId, partyId, cancellationToken);
        var identifiers = includeIdentifiers ? await ReadIdentifiersAsync(connection, transaction, tenantId, partyId, cancellationToken) : [];
        var contacts = await ReadContactsAsync(connection, transaction, tenantId, partyId, cancellationToken);
        var addresses = await ReadAddressesAsync(connection, transaction, tenantId, partyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return details with { Roles = roles, Identifiers = identifiers, ContactPoints = contacts, Addresses = addresses };
    }

    public async Task<IReadOnlyList<DuplicateCandidateDto>> DuplicatesAsync(TenantId tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id,candidate_party_id,reason,confidence,status from foundation_directory.duplicate_candidate where tenant_id=$1 and party_id=$2 order by created_at desc", connection, transaction);
        Add(command, tenantId.Value, partyId); var result = new List<DuplicateCandidateDto>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4)));
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<PartyReadinessDto?> ReadinessAsync(TenantId tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        var party = await GetAsync(tenantId, partyId, true, cancellationToken); if (party is null) return null;
        var missing = new List<string>(); if (party.Roles.Count == 0) missing.Add("role"); if (string.IsNullOrWhiteSpace(party.DisplayName)) missing.Add("display_name");
        IReadOnlyList<string> warnings = party.Identifiers.Count == 0 ? new[] { "identifier_missing" } : Array.Empty<string>(); return new(party.Id, missing.Count == 0, missing, warnings);
    }

    private static void Validate(CreatePartyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new DirectoryException("directory.display_name_required", "Display name is required.");
        if (request.DisplayName.Length > 300) throw new DirectoryException("directory.display_name_too_long", "Display name is too long.");
        if (request.PartyType == PartyType.Organization && (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))) throw new DirectoryException("directory.person_name_for_organization", "Person name fields are not valid for an organization.");
    }

    private static async Task<object?> ReadIdentityAuditSnapshotAsync(ITransactionContext context, TenantId tenantId, Guid partyId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select display_name,legal_name,trade_name,name_ar,name_en,first_name,middle_name,last_name,country_code,primary_language from foundation_directory.party where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction);
        Add(command, tenantId.Value, partyId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Dictionary<string, object?>
        {
            ["displayName"] = reader.GetString(0), ["legalName"] = Text(reader, 1), ["tradeName"] = Text(reader, 2),
            ["nameAr"] = Text(reader, 3), ["nameEn"] = Text(reader, 4), ["firstName"] = Text(reader, 5),
            ["middleName"] = Text(reader, 6), ["lastName"] = Text(reader, 7), ["countryCode"] = Text(reader, 8),
            ["primaryLanguage"] = Text(reader, 9)
        };
    }

    private static async Task<(Guid PartyId, string Reason, decimal Confidence)?> FindDuplicateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CreatePartyRequest request, CancellationToken ct)
    {
        foreach (var identifier in request.Identifiers ?? [])
        {
            await using var command = new NpgsqlCommand("select party_id from foundation_directory.party_identifier where tenant_id=$1 and identifier_type=$2 and normalized_value=$3 limit 1", connection, transaction); Add(command, tenantId.Value, identifier.Type, DirectoryNormalization.Identifier(identifier.Value));
            if (await command.ExecuteScalarAsync(ct) is Guid partyId) return (partyId, $"identifier:{identifier.Type}", 0.99m);
        }
        foreach (var contact in request.ContactPoints ?? [])
        {
            var normalized = contact.Type.Equals("Email", StringComparison.OrdinalIgnoreCase) ? DirectoryNormalization.Email(contact.Value) : DirectoryNormalization.Phone(contact.Value);
            await using var command = new NpgsqlCommand("select party_id from foundation_directory.contact_point where tenant_id=$1 and contact_type=$2 and normalized_value=$3 and party_id is not null limit 1", connection, transaction); Add(command, tenantId.Value, contact.Type, normalized);
            if (await command.ExecuteScalarAsync(ct) is Guid partyId) return (partyId, $"contact:{contact.Type}", 0.98m);
        }
        return null;
    }

    private static async Task InsertIdentifierAsync(ITransactionContext context, TenantId tenantId, Guid partyId, IdentifierInput input, CancellationToken ct) => await ExecuteAsync(context, "insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,authority,is_primary,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", ct, tenantId.Value, Guid.CreateVersion7(), partyId, input.Type, Null(input.CountryCode), input.Value.Trim(), DirectoryNormalization.Identifier(input.Value), Null(input.Authority), input.IsPrimary, DateTimeOffset.UtcNow);
    private static async Task InsertContactPointAsync(ITransactionContext context, TenantId tenantId, Guid partyId, ContactPointInput input, CancellationToken ct) { var normalized = input.Type.Equals("Email", StringComparison.OrdinalIgnoreCase) ? DirectoryNormalization.Email(input.Value) : DirectoryNormalization.Phone(input.Value); await ExecuteAsync(context, "insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_type,value,normalized_value,label,is_primary,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9)", ct, tenantId.Value, Guid.CreateVersion7(), partyId, input.Type, input.Value.Trim(), normalized, Null(input.Label), input.IsPrimary, DateTimeOffset.UtcNow); }
    private static async Task InsertAddressAsync(ITransactionContext context, TenantId tenantId, Guid partyId, AddressInput input, CancellationToken ct) { var addressId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; await ExecuteAsync(context, "insert into foundation_directory.address (tenant_id,id,country_code,city,region,postal_code,line1,line2,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$9)", ct, tenantId.Value, addressId, input.CountryCode, Null(input.City), Null(input.Region), Null(input.PostalCode), input.Line1, Null(input.Line2), now); await ExecuteAsync(context, "insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default) values ($1,$2,$3,$4,$5)", ct, tenantId.Value, partyId, addressId, input.Usage, input.IsDefault); }

    private static async Task AuditAsync(ITransactionContext context, TenantId tenantId, Guid actorId, Guid membershipId, Guid entityId, string action, object? before, object? after, Guid correlationId, DateTimeOffset now, CancellationToken ct) => await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenantId, null, null, "Human", actorId, membershipId, "directory", action, "party", entityId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), null, "directory-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), ct);
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return await command.ExecuteNonQueryAsync(ct); }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Text(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); Add(command, tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<IReadOnlyList<PartyRoleDto>> ReadRolesAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct) { await using var command = new NpgsqlCommand("select role_code,status,version from foundation_directory.party_role where tenant_id=$1 and party_id=$2 order by role_code", c, t); Add(command, tenantId.Value, partyId); var list = new List<PartyRoleDto>(); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetString(0), r.GetString(1), r.GetInt64(2))); return list; }
    private static async Task<IReadOnlyList<PartyIdentifierDto>> ReadIdentifiersAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,identifier_type,country_code,value,authority,is_primary,verification_status from foundation_directory.party_identifier where tenant_id=$1 and party_id=$2 order by is_primary desc,identifier_type", c, t); Add(command, tenantId.Value, partyId); var list = new List<PartyIdentifierDto>(); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetString(1), Text(r, 2), r.GetString(3), Text(r, 4), r.GetBoolean(5), r.GetString(6))); return list; }
    private static async Task<IReadOnlyList<ContactPointDto>> ReadContactsAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,contact_type,value,label,is_primary from foundation_directory.contact_point where tenant_id=$1 and party_id=$2 order by is_primary desc,contact_type", c, t); Add(command, tenantId.Value, partyId); var list = new List<ContactPointDto>(); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), Text(r, 3), r.GetBoolean(4))); return list; }
    private static async Task<IReadOnlyList<AddressDto>> ReadAddressesAsync(NpgsqlConnection c, NpgsqlTransaction t, TenantId tenantId, Guid partyId, CancellationToken ct) { await using var command = new NpgsqlCommand("select a.id,pa.usage,a.country_code,a.city,a.region,a.postal_code,a.line1,a.line2,pa.is_default from foundation_directory.party_address pa join foundation_directory.address a on a.tenant_id=pa.tenant_id and a.id=pa.address_id where pa.tenant_id=$1 and pa.party_id=$2 order by pa.is_default desc,a.id", c, t); Add(command, tenantId.Value, partyId); var list = new List<AddressDto>(); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), Text(r, 3), Text(r, 4), Text(r, 5), r.GetString(6), Text(r, 7), r.GetBoolean(8))); return list; }
}
