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
                await context.ExecuteAsync("insert into foundation_directory.party_role (tenant_id,party_id,role_code,status,created_by,created_at,updated_at,version) values ($1,$2,$3,$4,$5,$6,$6,1) on conflict (tenant_id,party_id,role_code) do update set status=excluded.status,updated_at=excluded.updated_at,version=foundation_directory.party_role.version+1", cancellationToken, context.Tenant.Id, party.Id, role.Code, role.Status, context.AdministratorId, context.AsOf);

            if (party.Index % 3 != 1)
            {
                var pointId = SeedIds.Child(context.Tenant.Id, "contact", party.Index);
                var email = $"contact-{party.Index:000}@{context.Tenant.LoginDomain}";
                await context.ExecuteAsync("insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_person_id,contact_type,value,normalized_value,label,is_primary,created_at) values ($1,$2,$3,null,'Email',$4,$5,'البريد الرئيسي',true,$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,label=excluded.label", cancellationToken, context.Tenant.Id, pointId, party.Id, email, email.ToLowerInvariant(), context.AsOf);
            }
            if (party.Index % 4 != 2)
            {
                var pointId = SeedIds.Child(context.Tenant.Id, "phone", party.Index);
                var phone = $"+2010{(party.Index + 10000000).ToString(CultureInfo.InvariantCulture)}";
                await context.ExecuteAsync("insert into foundation_directory.contact_point (tenant_id,id,party_id,contact_person_id,contact_type,value,normalized_value,label,is_primary,created_at) values ($1,$2,$3,null,'Phone',$4,$5,'الهاتف الرئيسي',true,$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,label=excluded.label", cancellationToken, context.Tenant.Id, pointId, party.Id, phone, phone, context.AsOf);
            }
            if (party.Index % 5 != 3)
            {
                var addressId = SeedIds.Child(context.Tenant.Id, "address", party.Index);
                await context.ExecuteAsync("insert into foundation_directory.address (tenant_id,id,country_code,city,region,postal_code,line1,line2,created_at,updated_at) values ($1,$2,'EG',$3,$4,$5,$6,$7,$8,$8) on conflict (tenant_id,id) do update set city=excluded.city,region=excluded.region,postal_code=excluded.postal_code,line1=excluded.line1,line2=excluded.line2,updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, addressId, party.Index % 2 == 0 ? "القاهرة" : "الإسكندرية", party.Index % 2 == 0 ? "القاهرة" : "الإسكندرية", $"11{party.Index:0000}", $"شارع التجارة رقم {party.Index + 1}، المنطقة الصناعية", party.Index % 2 == 0 ? "الدور الأول" : (object)DBNull.Value, context.AsOf);
                await context.ExecuteAsync("insert into foundation_directory.party_address (tenant_id,party_id,address_id,usage,is_default) values ($1,$2,$3,'General',true) on conflict (tenant_id,party_id,address_id,usage) do update set is_default=true", cancellationToken, context.Tenant.Id, party.Id, addressId);
            }
            if (party.Index % 6 == 0)
            {
                var identifierId = SeedIds.Child(context.Tenant.Id, "identifier", party.Index);
                var value = $"AT-{context.Tenant.Id.ToString()[..4].ToUpperInvariant()}-{party.Index:0000}";
                await context.ExecuteAsync("insert into foundation_directory.party_identifier (tenant_id,id,party_id,identifier_type,country_code,value,normalized_value,authority,valid_from,is_primary,verification_status,created_at) values ($1,$2,$3,'InternalReference','EG',$4,$5,'Development Seed','2026-01-01',true,'Verified',$6) on conflict (tenant_id,id) do update set value=excluded.value,normalized_value=excluded.normalized_value,verification_status=excluded.verification_status", cancellationToken, context.Tenant.Id, identifierId, party.Id, value, value, context.AsOf);
            }
        }
        return new(parties.Count, parties.Count(party => party.Status == "Active"), parties.Count(party => party.Status == "Archived"), parties.Any(party => party.Roles.Count == 2), parties.Any(party => party.Index % 3 == 1), parties.Any(party => party.Index % 4 == 2));
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
            if (character is >= '\u0300' and <= '\u036f') continue;
            builder.Append(character switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ؤ' => 'و', 'ئ' => 'ي', 'ـ' => '\0', _ => character });
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record PartySpec(Guid Id, int Index, string Type, string Status, string DisplayName, string? LegalName, string? TradeName, string? NameAr, string? NameEn, string? FirstName, string? MiddleName, string? LastName, IReadOnlyList<PartyRole> Roles);
    private sealed record PartyRole(string Code, string Status);
}

public sealed record SeedModuleSummary(int Total, int Active, int InactiveOrArchived, bool HasCombinedRoles, bool HasOptionalContact, bool HasOptionalAddress);
