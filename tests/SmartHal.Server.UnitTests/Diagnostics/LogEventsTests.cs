using System.Reflection;
using AwesomeAssertions;
using SmartHal.Server.Diagnostics;
using Xunit;

namespace SmartHal.Server.UnitTests.Diagnostics;

/// <summary>
/// Verifies that every <c>EventId</c> constant of <see cref="LogEvents"/> stays inside the block
/// slice 0 owns and inside the range of its area (IF-6, FR-34).
/// </summary>
public sealed class LogEventsTests
{
    private const int SliceZeroFirstEventId = 1000;

    private const int SliceZeroLastEventId = 1999;

    // The area is taken from the name prefix of the nested class that groups the constants; the
    // names mirror the "Bereich" column of the EventId table (IF-6).
    private static readonly (string Area, int First, int Last)[] AreaRanges =
    [
        ("Lifecycle", 1000, 1099),
        ("Configuration", 1100, 1199),
        ("Health", 1200, 1299)
    ];

    [Fact]
    public void LogEvents_AllEventIds_LieInSliceZeroBlockAndTheirRange()
    {
        // Arrange
        var eventIds = EventIdsOfLogEvents();

        // Act
        var outsideSliceZero = eventIds
            .Where(eventId => eventId.Value is < SliceZeroFirstEventId or > SliceZeroLastEventId)
            .Select(eventId => $"{eventId.Area}.{eventId.Name} = {eventId.Value}")
            .ToArray();
        var outsideArea = eventIds
            .Where(eventId => !IsInAreaRange(eventId.Area, eventId.Value))
            .Select(eventId => $"{eventId.Area}.{eventId.Name} = {eventId.Value}")
            .ToArray();
        var duplicates = eventIds
            .GroupBy(eventId => eventId.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        // Assert
        eventIds.Should().NotBeEmpty();
        eventIds.Select(eventId => eventId.Area).Distinct(StringComparer.Ordinal)
            .Should().BeSubsetOf(AreaRanges.Select(range => range.Area));
        outsideSliceZero.Should().BeEmpty("slice 0 owns the EventId block 1000-1999 (IF-6)");
        outsideArea.Should().BeEmpty("every EventId belongs to the range of its area (IF-6)");
        duplicates.Should().BeEmpty("an EventId identifies exactly one event (FR-34)");
    }

    private static bool IsInAreaRange(string area, int value)
    {
        var range = Array.Find(AreaRanges, candidate => string.Equals(candidate.Area, area, StringComparison.Ordinal));

        return range.Area is not null && value >= range.First && value <= range.Last;
    }

    private static (string Area, string Name, int Value)[] EventIdsOfLogEvents()
    {
        return typeof(LogEvents)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(area => area
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(int))
                .Select(field => (Area: area.Name, field.Name, Value: (int)field.GetRawConstantValue()!)))
            .ToArray();
    }
}
