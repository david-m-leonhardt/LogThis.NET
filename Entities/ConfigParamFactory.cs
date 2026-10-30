using LogThis.Entities.ConfigParameters;
using LogThis.Enum;
using LogThis.Interfaces;

namespace LogThis.Entities
{
    /// <summary>Builds configured event components in their rendered order.</summary>
    internal static class ConfigParamFactory
    {
        #region Internal Methods

        /// <summary>Captures eligible payloads before preparing correlation and other event components.</summary>
        /// <param name="runtimeValues">Event kind, invocation data, and selected runtime configuration.</param>
        /// <returns>Ordered components whose inclusion flags determine what is emitted.</returns>
        /// <remarks>The caller checks event and log-level eligibility before constructing components.</remarks>
        internal static List<IConfigParameter> BuildConfigParameters(LogThisRuntimeValues runtimeValues)
        {
            CapturedArgumentsConfigParameter capturedArguments = new(runtimeValues);

            CapturedReturnConfigParameter? capturedReturn =
                runtimeValues.AccessPoint.EventKind == AccessPointEventKind.Entry
                    ? null
                    : new CapturedReturnConfigParameter(runtimeValues);

            List<IConfigParameter> configParams =
            [
                new AccessPointConfigParameter(runtimeValues),
                new CorrelationIdConfigParameter(runtimeValues),
                new TraceIdConfigParameter(runtimeValues),
                new SpanIdConfigParameter(runtimeValues),
                new DurationConfigParameter(runtimeValues),
                new ClassNameConfigParameter(runtimeValues),
                new MethodNameConfigParameter(runtimeValues),
                capturedArguments
            ];

            if (capturedReturn != null)
            {
                configParams.Add(capturedReturn);
            }

            return configParams;
        }

        #endregion
    }
}
