using System.Text.Json;
using NodaTime;
using Shared.BuildingBlocks.Errors;
using Shared.Primitives.Errors;
using Shared.Primitives.Identifiers;
using Shared.Primitives.References;
using Shared.Primitives.Time;
using Xunit;

namespace Shared.Primitives.Tests;

public sealed class SharedPrimitivesTests
{
    [Fact]
    public void PlatformIdentifiersUseUuidVersion7()
    {
        var identifiers = new Guid[]
        {
            TenantId.New().Value,
            LegalEntityId.New().Value,
            BranchId.New().Value,
            IdentityId.New().Value,
            MembershipId.New().Value,
            UserId.New().Value,
            ModuleId.New().Value,
            CorrelationId.New().Value,
            CausationId.New().Value,
            MessageId.New().Value
        };

        Assert.All(identifiers, identifier => Assert.Equal(7, identifier.Version));
    }

    [Fact]
    public void IdentifierAndReferencesSerializeWithoutLosingIdentity()
    {
        var tenantId = TenantId.New();
        var entityRef = new EntityRef("party", tenantId.Value);
        var actorRef = new ActorRef("user", UserId.New().Value);

        var serialized = JsonSerializer.Serialize(new { tenantId, entityRef, actorRef });

        Assert.Contains(tenantId.Value.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(entityRef.Id.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actorRef.Id.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstantConvertsToEntityLocalBusinessDateWithoutServerLocalTime()
    {
        var entityTimeZone = DateTimeZoneProviders.Tzdb["Africa/Cairo"];
        var instant = Instant.FromUtc(2026, 1, 1, 22, 30);

        var businessDate = BusinessTime.ToLocalDate(instant, entityTimeZone);
        var startOfDay = BusinessTime.StartOfDay(businessDate, entityTimeZone);

        Assert.Equal(new LocalDate(2026, 1, 2), businessDate);
        Assert.Equal(Instant.FromUtc(2026, 1, 1, 22, 0), startOfDay);
    }

    [Fact]
    public void DomainErrorsMapToStableProblemDetails()
    {
        DomainError error = new ValidationError(
            "tenant.not_found",
            "The tenant was not found.",
            new Dictionary<string, string[]> { ["tenantId"] = ["Unknown tenant."] });

        var problemDetails = ProblemDetailsMapper.ToProblemDetails(error);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("tenant.not_found", problemDetails.Extensions["code"]);
        Assert.Equal("Validation failed", problemDetails.Title);
        Assert.True(problemDetails.Extensions.ContainsKey("errors"));
    }
}
