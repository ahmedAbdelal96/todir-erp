using Core.Messaging;
using Shared.Primitives.Identifiers;
using Xunit;

namespace CoreTests;

public sealed class InboxStoreTests
{
    [Fact]
    public async Task AbandonedMessageCanBeRetriedButProcessedMessageCannot()
    {
        var store = new InMemoryInboxStore();
        var tenantId = new TenantId(Guid.CreateVersion7());
        var messageId = Guid.CreateVersion7();

        Assert.True(await store.TryBeginAsync("catalog-import-worker", messageId, tenantId));
        Assert.False(await store.TryBeginAsync("catalog-import-worker", messageId, tenantId));
        await store.AbandonAsync("catalog-import-worker", messageId, tenantId);
        Assert.True(await store.TryBeginAsync("catalog-import-worker", messageId, tenantId));
        await store.MarkProcessedAsync("catalog-import-worker", messageId, tenantId);
        await store.AbandonAsync("catalog-import-worker", messageId, tenantId);
        Assert.False(await store.TryBeginAsync("catalog-import-worker", messageId, tenantId));
    }
}