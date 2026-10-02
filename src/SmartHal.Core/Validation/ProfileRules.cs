using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks the capabilities of a channel or channel template against its profile (R14).
/// </summary>
internal static class ProfileRules
{
    /// <summary>
    /// Checks that the capability types of a channel satisfy a profile.
    /// </summary>
    /// <remarks>
    /// A required capability has to be present at least once and at least <see cref="ProfileCapability.Min"/> times;
    /// an optional one may be absent, but when present its count has to stay within the minimum and maximum.
    /// </remarks>
    /// <param name="profile">The profile the channel follows.</param>
    /// <param name="types">The capability type of every capability of the channel.</param>
    /// <param name="path">The path of the channel.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void Check(ChannelProfile profile, IReadOnlyList<TypeRef> types, string path, ValidationContext context)
    {
        if (ValidationContext.IsNull(profile.Capabilities))
        {
            return;
        }

        foreach (var expected in profile.Capabilities)
        {
            if (ValidationContext.IsNull(expected))
            {
                continue;
            }

            var count = types.Count(type => type == expected.Type);
            var problem = Describe(expected, count);

            if (problem is not null)
            {
                context.Add(path, ValidationCodes.ProfileViolation, $"The profile '{profile.Name}' {problem}");
            }
        }
    }

    private static string? Describe(ProfileCapability expected, int count)
    {
        if (count == 0)
        {
            return expected.Required ? $"requires the capability '{expected.Type}', which is missing." : null;
        }

        if (expected.Min is { } min && count < min)
        {
            return $"requires the capability '{expected.Type}' at least {min} times, but it occurs {count} times.";
        }

        if (expected.Max is { } max && count > max)
        {
            return $"allows the capability '{expected.Type}' at most {max} times, but it occurs {count} times.";
        }

        return null;
    }
}
