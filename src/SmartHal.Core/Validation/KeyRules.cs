using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks keys within their scope: exactly one root channel and no duplicate keys (R8).
/// </summary>
internal static class KeyRules
{
    /// <summary>
    /// The key of the root channel every device has.
    /// </summary>
    public const string RootChannelKey = "0";

    /// <summary>
    /// Checks that a list of channels has exactly one root channel and no duplicate keys.
    /// </summary>
    /// <typeparam name="T">The type of the channels.</typeparam>
    /// <param name="channels">The present channels with their index.</param>
    /// <param name="key">Returns the key of a channel.</param>
    /// <param name="path">The path of the channel list.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckChannels<T>(
        IReadOnlyList<(T Item, int Index)> channels,
        Func<T, string?> key,
        string path,
        ValidationContext context)
    {
        var roots = channels.Count(channel => key(channel.Item) == RootChannelKey);

        if (roots != 1)
        {
            context.Add(
                path,
                ValidationCodes.RootChannel,
                roots == 0
                    ? $"There is no root channel '{RootChannelKey}'."
                    : $"There are {roots} root channels '{RootChannelKey}'; exactly one is required.");
        }

        CheckUnique(channels, key, path, "channel", context);
    }

    /// <summary>
    /// Checks that no key occurs twice in a list; every repetition is reported at its <c>key</c> member.
    /// </summary>
    /// <typeparam name="T">The type of the entries.</typeparam>
    /// <param name="entries">The present entries with their index.</param>
    /// <param name="key">Returns the key of an entry.</param>
    /// <param name="path">The path of the list.</param>
    /// <param name="kind">The kind of entry named in the message, for example <c>channel</c>.</param>
    /// <param name="context">The context the violations are reported to.</param>
    public static void CheckUnique<T>(
        IReadOnlyList<(T Item, int Index)> entries,
        Func<T, string?> key,
        string path,
        string kind,
        ValidationContext context)
    {
        var seen = new HashSet<string?>(StringComparer.Ordinal);

        foreach (var (item, i) in entries)
        {
            var value = key(item);

            if (!seen.Add(value))
            {
                context.Add(
                    ValidationContext.Member(ValidationContext.Index(path, i), "key"),
                    ValidationCodes.DuplicateKey,
                    $"The {kind} key '{value}' occurs more than once.");
            }
        }
    }
}
