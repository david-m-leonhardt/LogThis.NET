using LogThis.Constants;

namespace LogThis.Extensions
{
    /// <summary>Helpers for message-template component names.</summary>
    public static class StringExtensions
    {
        #region Private Fields

        // Use the same placeholders as the runtime rather than reserving the entire LogThis prefix.
        private static readonly HashSet<string> ReservedNames = new(
            new[]
            {
                MessageComponents.OnEntryLabel,
                MessageComponents.OnExitLabel,
                MessageComponents.OnExceptionLabel,
                MessageComponents.LogThisClass,
                MessageComponents.LogThisMethod,
                MessageComponents.LogThisArguments,
                MessageComponents.LogThisReturnValue,
                MessageComponents.LogThisDurationMs,
                MessageComponents.LogThisCorrelationId,
                MessageComponents.LogThisTraceId,
                MessageComponents.LogThisSpanId
            }.Select(placeholder => placeholder[1..^1]), StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Extension Block

        extension(string source)
        {
            /// <summary>Validates and wraps a custom name as one message-template placeholder.</summary>
            /// <returns>The normalized placeholder.</returns>
            /// <exception cref="ArgumentException">The name is malformed or reserved by LogThis.</exception>
            public string PrepComponentName()
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(source);
                string name = source.Trim();
                if (name.StartsWith('{') && name.EndsWith('}'))
                {
                    name = name[1..^1];
                }

                if (string.IsNullOrWhiteSpace(name) || name.Contains('{') || name.Contains('}'))
                {
                    throw new ArgumentException($"Message component '{source}' must be a bare name or a single brace-wrapped placeholder. Choose another name.");
                }

                // Alignment/format suffixes and provider hints cannot bypass reservations.
                int suffix = name.IndexOfAny([',', ':']);
                string propertyName = (suffix < 0 ? name : name[..suffix]).Trim().TrimStart('@', '$').Trim();

                if (propertyName.Length == 0)
                {
                    throw new ArgumentException($"Message component '{source}' has no property name. Choose another name.");
                }

                if (ReservedNames.Contains(propertyName))
                {
                    throw new ArgumentException($"Message component '{source}' conflicts with reserved LogThis property '{propertyName}'. Choose another name.");
                }

                return "{" + name + "}";
            }
        }

        #endregion
    }
}
