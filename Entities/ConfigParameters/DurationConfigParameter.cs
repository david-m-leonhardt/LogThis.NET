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
        /// <param name="durationMs">Previously measured elapsed milliseconds, or null when timing is disabled.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public DurationConfigParameter(double? durationMs, LogThisRuntime runtime) : base(runtime)
        {
            if (durationMs is double duration)
            {
                Obj = duration;
                ShouldAddValue = true;
            }
        }

        #endregion
    }
}
