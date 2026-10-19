using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Npgsql;

namespace Worker;

/// <summary>Rebuilds search rows from current Catalog truth; replay is safe even for older events.</summary>
public sealed class CatalogProjectionConsumer(NpgsqlDataSource dataSource, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "catalog-search-projection";
    private static readonly HashSet<string> RebuildEvents = new(StringComparer.Ordinal)
    {
        "CatalogProductCreatedV1", "CatalogProductChangedV1", "CatalogProductArchivedV1", "CatalogProductRestoredV1",
        "CatalogProductVariantCreatedV1", "CatalogProductUomChangedV1", "CatalogProductBarcodeChangedV1",
        "CatalogProductTranslationChangedV1", "CatalogProductAttributesChangedV1"
    };

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            if (RebuildEvents.Contains(message.MessageType))
                await RebuildAsync(message.TenantId.Value, message.Payload, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private async Task RebuildAsync(Guid tenantId, string payload, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var templateId = ReadGuid(root, "templateId") ?? ReadGuid(root, "productId");
        var variantId = ReadGuid(root, "variantId");
        if (templateId is null && variantId is null) return;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var lockKey = $"catalog-projection:{tenantId:D}:{templateId?.ToString("D") ?? variantId!.Value.ToString("D")}";
        await using (var aggregateLock = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction))
        {
            aggregateLock.Parameters.AddWithValue(lockKey);
            await aggregateLock.ExecuteNonQueryAsync(cancellationToken);
        }
        if (templateId is not null)
        {
            await using var delete = new NpgsqlCommand("delete from foundation_catalog.catalog_search_projection where tenant_id=$1 and template_id=$2", connection, transaction);
            delete.Parameters.AddWithValue(tenantId); delete.Parameters.AddWithValue(templateId.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken);
            const string insertSql = "insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and p.id=$2";
            await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(templateId.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var delete = new NpgsqlCommand("delete from foundation_catalog.catalog_search_projection where tenant_id=$1 and variant_id=$2", connection, transaction);
            delete.Parameters.AddWithValue(tenantId); delete.Parameters.AddWithValue(variantId!.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken);
            const string insertSql = "insert into foundation_catalog.catalog_search_projection (tenant_id,variant_id,template_id,display_name,display_name_normalized,name_ar,name_en,sku,normalized_sku,category_id,category_name,base_uom_code,product_nature,status,updated_at) select p.tenant_id,v.id,p.id,p.display_name,p.display_name_normalized,p.name_ar,p.name_en,v.sku,v.normalized_sku,p.category_id,c.name,u.code,p.product_nature,p.status,p.updated_at from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id left join foundation_catalog.product_category c on c.tenant_id=p.tenant_id and c.id=p.category_id left join foundation_catalog.product_uom pu on pu.tenant_id=v.tenant_id and pu.variant_id=v.id and pu.is_base and pu.status='Active' and pu.effective_to is null left join foundation_catalog.uom u on u.tenant_id=pu.tenant_id and u.id=pu.uom_id where p.tenant_id=$1 and v.id=$2";
            await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue(tenantId); insert.Parameters.AddWithValue(variantId.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static Guid? ReadGuid(JsonElement root, string propertyName) => root.TryGetProperty(propertyName, out var value) && value.TryGetGuid(out var parsed) ? parsed : null;
}

public sealed class CatalogProjectionConsumerDefinition : ConsumerDefinition<CatalogProjectionConsumer>
{
    public CatalogProjectionConsumerDefinition() => EndpointName = CatalogProjectionConsumer.EndpointName;
}
