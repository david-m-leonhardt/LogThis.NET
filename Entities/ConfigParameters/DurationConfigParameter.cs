using LogThis.Constants;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Includes an already measured elapsed duration as a numeric component.</summary>
    internal sealed class DurationConfigParameter : ConfigParameter
    {
        #region Public Properties

        /// <inheritdoc />
        public override string Message => MessageComponents.LogThisDurationMs;

        #endregion

        #region Constructor

        /// <summary>Uses the supplied milliseconds without starting or stopping a timer.</summary>
        /// <param name="runtimeValues">Event data containing elapsed milliseconds measured before formatting.</param>
        public DurationConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            if (runtimeValues.DurationMs is double duration)
            {
                Obj = duration;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
