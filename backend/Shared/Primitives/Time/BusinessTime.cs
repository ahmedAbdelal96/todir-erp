using NodaTime;

namespace Shared.Primitives.Time;

/// <summary>Converts an instant to an entity-local business date without using server local time.</summary>
public static class BusinessTime
{
    public static LocalDate ToLocalDate(Instant instant, DateTimeZone entityTimeZone) =>
        instant.InZone(entityTimeZone).Date;

    public static Instant StartOfDay(LocalDate businessDate, DateTimeZone entityTimeZone) =>
        entityTimeZone.AtStartOfDay(businessDate).ToInstant();
}
