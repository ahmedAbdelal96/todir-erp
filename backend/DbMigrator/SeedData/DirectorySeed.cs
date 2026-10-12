using System.Globalization;
using System.Text;

namespace DbMigrator.SeedData;

public static class DirectorySeed
{
    public static async Task<SeedModuleSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var parties = BuildParties(context.Tenant, context.Tenant.Id == SeedIds.AtlasTenant ? 80 : 55);
        foreach (var party in parties)
        {
            await context.ExecuteAsync("insert into foundation_directory.party (tenant_id,id,party_type,status,display_name,display_name_normalized,legal_name,trade_name,name_ar,name_en,first_name,middle_name,last_name,country_code,primary_language,created_by,updated_by,created_at,updated_at,version) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,'EG',$14,$15,$15,$16,$16,1) on conflict (tenant_id,id) do update set party_type=excluded.party_type,status=excluded.status,display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,legal_name=excluded.legal_name,trade_name=excluded.trade_name,name_ar=excluded.name_ar,name_en=excluded.name_en,first_name=excluded.first_name,middle_name=excluded.middle_name,last_name=excluded.last_name,primary_language=excluded.primary_language,updated_by=excluded.updated_by,updated_at=excluded.updated_at,version=foundation_directory.party.version+1", cancellationToken, context.Tenant.Id, party.Id, party.Type, party.Status, party.DisplayName, Normalize(party.DisplayName), party.LegalName ?? (object)DBNull.Value, party.TradeName ?? (object)DBNull.Value, party.NameAr ?? (object)DBNull.Value, party.NameEn ?? (object)DBNull.Value, party.FirstName ?? (object)DBNull.Value, party.MiddleName ?? (object)DBNull.Value, party.LastName ?? (object)DBNull.Value, "ar", context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("insert into foundation_directory.party_visibility (tenant_id,party_id,scope_type,legal_entity_id,branch_id,visibility_id) values ($1,$2,'TenantWide',null,null,$3) on conflict do nothing", cancellationToken, context.Tenant.Id, party.Id, SeedIds.Child(context.Tenant.Id, "party-visibility", party.Index));
            foreach (var role in party.Roles)
                await context.ExecuteAsync("insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version,active_from) values ($1,$2,$3,$4,$5,$6,$6,1,$6::date) on conflict (tenant_id,party_id,role_code) do update set status=excluded.status,updated_at=excluded.updated_at,version=foundation_directory.party_role.version+1,active_from=coalesce(foundation_directory.party_role.active_from,excluded.active_from)", cancellationToken, context.Tenant.Id, party.Id, role.Code, role.Status, context.AdministratorId, context.AsOf);

            if (party.Index % 3 != 1)
            {
                var pointId = SeedIds.Child(context.Tenant.Id, "contact", party.Index);
                var email = $"contact-{party.Index:000}@{context.Tenant.LoginDomain}";
                await context.ExecuteAsync("insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_person_id,contact_type,value,normalized_value,label,is_primary,status,created_at) values ($1,$2,$3,null,'Email',$4,$5,'البريد الرئيسي',true,'Active',$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,label=excluded.label,status='Active'", cancellationToken, context.Tenant.Id, pointId, party.Id, email, email.ToLowerInvariant(), context.AsOf);
            }
            if (party.Index % 4 != 2)
            {
                var pointId = SeedIds.Child(context.Tenant.Id, "phone", party.Index);
                var phone = $"+2010{(party.Index + 10000000).ToString(CultureInfo.InvariantCulture)}";
                await context.ExecuteAsync("insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_person_id,contact_type,value,normalized_value,label,is_primary,status,created_at) values ($1,$2,$3,null,'Phone',$4,$5,'الهاتف الرئيسي',true,'Active',$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,label=excluded.label,status='Active'", cancellationToken, context.Tenant.Id, pointId, party.Id, phone, phone, context.AsOf);
            }
            if (party.Index % 5 != 3)
            {
                var addressId = SeedIds.Child(context.Tenant.Id, "address", party.Index);
                await context.ExecuteAsync("insert into foundation_directory.address (tenant_id,id,country_code,city,region,postal_code,line1,line2,address_type,status,version,created_at,updated_at) values ($1,$2,'EG',$3,$4,$5,$6,$7,'General','Active',1,$8,$8) on conflict (tenant_id,id) do update set city=excluded.city,region=excluded.region,postal_code=excluded.postal_code,line1=excluded.line1,line2=excluded.line2,status='Active',updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, addressId, party.Index % 2 == 0 ? "القاهرة" : "الإسكندرية", party.Index % 2 == 0 ? "القاهرة" : "الإسكندرية", $"11{party.Index:0000}", $"شارع التجارة رقم {party.Index + 1}، المنطقة الصناعية", party.Index % 2 == 0 ? "الدور الأول" : (object)DBNull.Value, context.AsOf);
                await context.ExecuteAsync("insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default) values ($1,$2,$3,'General',true) on conflict (tenant_id,party_id,address_id,usage) do update set is_default=true", cancellationToken, context.Tenant.Id, party.Id, addressId);
            }
            if (party.Index % 6 == 0)
            {
                var identifierId = SeedIds.Child(context.Tenant.Id, "identifier", party.Index);
                var value = $"AT-{context.Tenant.Id.ToString()[..4].ToUpperInvariant()}-{party.Index:0000}";
                await context.ExecuteAsync("insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,authority,valid_from,is_primary,verification_status,created_at) values ($1,$2,$3,'InternalReference','EG',$4,$5,'Development Seed','2026-01-01',true,'Verified',$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,verification_status=excluded.verification_status", cancellationToken, context.Tenant.Id, identifierId, party.Id, value, value, context.AsOf);
            }

            if (party.Index <= 4)
            {
                var contactId = SeedIds.Child(context.Tenant.Id, "contact-person", party.Index);
                var contactName = party.Index % 2 == 0 ? "مسؤول الحساب" : "Account Owner";
                await context.ExecuteAsync("insert into foundation_directory.contact_person (tenant_id,id,display_name,display_name_normalized,job_title,department,language_code,status,version,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Active',1,$8,$8) on conflict (tenant_id,id) do update set display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,status='Active',updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, contactId, contactName, Normalize(contactName), "Account Manager", "Commercial", party.Index % 2 == 0 ? "ar" : "en", context.AsOf);
                await context.ExecuteAsync("insert into foundation_directory.party_contact (tenant_id,party_id,contact_person_id,relationship_type,is_primary,usage,status) values ($1,$2,$3,'AccountContact',$4,$5,'Active') on conflict (tenant_id,party_id,contact_person_id) do update set relationship_type=excluded.relationship_type,is_primary=excluded.is_primary,usage=excluded.usage,status='Active'", cancellationToken, context.Tenant.Id, party.Id, contactId, party.Index == 1, party.Index == 2 ? "Shipping" : "Billing");
                var contactPointId = SeedIds.Child(context.Tenant.Id, "contact-person-email", party.Index);
                var contactEmail = $"account-{party.Index:000}@{context.Tenant.LoginDomain}";
                await context.ExecuteAsync("insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_person_id,contact_type,value,normalized_value,label,is_primary,status,created_at) values ($1,$2,null,$3,'Email',$4,$5,'Contact person',true,'Active',$6) on conflict (tenant_id,id) do update set contact_person_id=excluded.contact_person_id,status='Active'", cancellationToken, context.Tenant.Id, contactPointId, contactId, contactEmail, contactEmail.ToLowerInvariant(), context.AsOf);
            }

            var aliasId = SeedIds.Child(context.Tenant.Id, "alias", party.Index);
            await context.ExecuteAsync("insert into foundation_directory.party_alias (tenant_id,id,party_id,alias,alias_normalized,alias_type,language_code,created_at) values ($1,$2,$3,$4,$5,'Alternate',$6,$7) on conflict (tenant_id,id) do update set alias=excluded.alias,alias_normalized=excluded.alias_normalized,language_code=excluded.language_code", cancellationToken, context.Tenant.Id, aliasId, party.Id, party.Index % 2 == 0 ? party.NameEn ?? party.DisplayName : party.NameAr ?? party.DisplayName, Normalize(party.Index % 2 == 0 ? party.NameEn ?? party.DisplayName : party.NameAr ?? party.DisplayName), party.Index % 2 == 0 ? "en" : "ar", context.AsOf);

            if (party.Index is 1 or 2)
            {
                var taxId = SeedIds.Child(context.Tenant.Id, "tax-id", party.Index);
                var countryPackPrefix = context.Tenant.Id == SeedIds.AtlasTenant ? "ATLAS" : "DELTA";
                var vatValue = $"VAT-{countryPackPrefix}-0001";
                await context.ExecuteAsync("insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,authority,valid_from,is_primary,verification_status,created_at) values ($1,$2,$3,'TaxIdentifier','EG',$4,$4,'Development Country Pack','2026-01-01',true,'Verified',$5) on conflict (tenant_id,id) do update set party_id=excluded.party_id,value=excluded.value,normalized_value=excluded.normalized_value,verification_status='Verified'", cancellationToken, context.Tenant.Id, taxId, party.Id, vatValue, context.AsOf);

                var registrationId = SeedIds.Child(context.Tenant.Id, "commercial-registration-id", party.Index);
                var registrationValue = $"CR-{countryPackPrefix}-0001";
                await context.ExecuteAsync("insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,authority,valid_from,is_primary,verification_status,created_at) values ($1,$2,$3,'CommercialRegistration','EG',$4,$4,'Development Commercial Registry','2026-01-01',false,'Verified',$5) on conflict (tenant_id,id) do update set party_id=excluded.party_id,value=excluded.value,normalized_value=excluded.normalized_value,verification_status='Verified'", cancellationToken, context.Tenant.Id, registrationId, party.Id, registrationValue, context.AsOf);
            }
        }
        await EnsureDirectoryScenariosAsync(context, parties, cancellationToken);
        return new(parties.Count, parties.Count(party => party.Status == "Active"), parties.Count(party => party.Status == "Archived"), parties.Any(party => party.Roles.Count == 2), parties.Any(party => party.Index % 3 == 1), parties.Any(party => party.Index % 4 == 2));
    }

    private static async Task EnsureDirectoryScenariosAsync(SeedContext context, IReadOnlyList<PartySpec> parties, CancellationToken cancellationToken)
    {
        var first = parties[0].Id;
        var legalEntity = context.EntityId;
        await context.ExecuteAsync("delete from foundation_directory.party_visibility where tenant_id=$1 and party_id=$2", cancellationToken, context.Tenant.Id, parties[1].Id);
        await context.ExecuteAsync("insert into foundation_directory.party_visibility (tenant_id,party_id,scope_type,legal_entity_id,visibility_id) values ($1,$2,'SelectedLegalEntities',$3,$4) on conflict do nothing", cancellationToken, context.Tenant.Id, parties[1].Id, legalEntity, SeedIds.Child(context.Tenant.Id, "restricted-visibility", 1));

        var billingId = SeedIds.Child(context.Tenant.Id, "billing-address", 1);
        var shippingId = SeedIds.Child(context.Tenant.Id, "shipping-address", 1);
        foreach (var address in new[]
        {
            (Id: billingId, Usage: "Billing", Line: "مقر الحسابات — شارع النيل", City: "القاهرة"),
            (Id: shippingId, Usage: "Shipping", Line: "مخزن الشحن — المنطقة الصناعية", City: "الجيزة")
        })
        {
            await context.ExecuteAsync("insert into foundation_directory.address (tenant_id,id,country_code,city,region,line1,address_type,status,version,created_at,updated_at) values ($1,$2,'EG',$3,$3,$4,$5,'Active',1,$6,$6) on conflict (tenant_id,id) do update set line1=excluded.line1,status='Active',updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, address.Id, address.City, address.Line, address.Usage, context.AsOf);
            await context.ExecuteAsync("insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default,valid_from) values ($1,$2,$3,$4,true,'2026-01-01') on conflict (tenant_id,party_id,address_id,usage) do update set is_default=true,valid_from='2026-01-01'", cancellationToken, context.Tenant.Id, first, address.Id, address.Usage);
        }
        var duplicateId = SeedIds.Child(context.Tenant.Id, "duplicate-candidate", 1);
        await context.ExecuteAsync("insert into foundation_directory.duplicate_candidate (tenant_id,id,party_id,candidate_party_id,reason,confidence,status,created_at,resolution_reason) values ($1,$2,$3,$4,'Same verified tax identifier',0.99,'Open',$5,null) on conflict (tenant_id,id) do update set reason=excluded.reason,confidence=excluded.confidence,status='Open',resolved_at=null,resolved_by=null,resolution_reason=null", cancellationToken, context.Tenant.Id, duplicateId, parties[0].Id, parties[1].Id, context.AsOf);
        await context.ExecuteAsync("insert into foundation_directory.external_reference (tenant_id,id,party_id,system_code,external_key,created_at) values ($1,$2,$3,'directory-import','ATLAS-001',$4) on conflict (tenant_id,system_code,external_key) do update set party_id=excluded.party_id", cancellationToken, context.Tenant.Id, SeedIds.Child(context.Tenant.Id, "external-reference", 1), first, context.AsOf);
    }

    private static List<PartySpec> BuildParties(SeedTenant tenant, int count)
    {
        var names = new[]
        {
            ("أطلس للتجارة العامة", "Atlas Trading General"), ("أطلس للصناعات المكتبية", "Atlas Office Industries"),
            ("النور للتوريدات", "Al Nour Supplies"), ("النور للصناعات", "Al Nour Industries"),
            ("شركة الرواد للتجهيزات", "Pioneers Equipment Co."), ("مؤسسة النخبة التجارية", "Elite Trading Establishment"),
            ("شركة المستقبل للأدوات الكهربائية", "Future Electrical Tools"), ("الصفوة لمستلزمات التعبئة", "Safwa Packaging Supplies"),
            ("مخازن الإمداد العربي", "Arab Supply Stores"), ("مكتب الخليج للخدمات", "Gulf Services Office")
        };
        var result = new List<PartySpec>(count);
        for (var index = 0; index < count; index++)
        {
            var humanIndex = index + 1;
            var named = index < names.Length ? names[index] : ($"شركة {new[] { "اليسر", "البركة", "التميز", "الوفاء", "المرجان", "المنارة" }[index % 6]} للتوريدات — فرع {humanIndex}", $"{new[] { "Al Yusr", "Al Baraka", "Tamayouz", "Al Wafaa", "Marjan", "Al Manara" }[index % 6]} Supplies Branch {humanIndex}");
            var isPerson = index % 7 == 0;
            var isArchived = index == count - 1;
            var display = isPerson ? new[] { "أحمد محمود", "سارة إبراهيم", "مريم حسن", "Omar Nabil" }[index % 4] : named.Item1;
            var type = isPerson ? "Person" : "Organization";
            var roles = index % 5 == 0 ? new[] { new PartyRole("Customer", "Active"), new PartyRole("Supplier", "Active") } : new[] { new PartyRole(index % 2 == 0 ? "Customer" : "Supplier", index == count - 2 ? "Inactive" : "Active") };
            result.Add(new(SeedIds.Party(tenant.Id, humanIndex), humanIndex, type, isArchived ? "Archived" : "Active", display, isPerson ? null : display, isPerson ? null : named.Item2, isPerson ? display : named.Item1, isPerson ? named.Item2 : null, isPerson ? display.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0] : null, isPerson ? "عميل" : null, isPerson ? display.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last() : null, roles));
        }
        return result;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant().Length);
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (character is >= '\u0300' and <= '\u036f' or >= '\u064B' and <= '\u065F' or '\u0670') continue;
            builder.Append(character switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ؤ' => 'و', 'ئ' => 'ي', 'ـ' => '\0', _ => character });
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record PartySpec(Guid Id, int Index, string Type, string Status, string DisplayName, string? LegalName, string? TradeName, string? NameAr, string? NameEn, string? FirstName, string? MiddleName, string? LastName, IReadOnlyList<PartyRole> Roles);
    private sealed record PartyRole(string Code, string Status);
}

public sealed record SeedModuleSummary(int Total, int Active, int InactiveOrArchived, bool HasCombinedRoles, bool HasOptionalContact, bool HasOptionalAddress);
