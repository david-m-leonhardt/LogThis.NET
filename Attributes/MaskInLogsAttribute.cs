namespace LogThis.Attributes
{
    /// <summary>Shares fixed or configuration-based replacement text across masking attributes.</summary>
    public abstract class MaskInLogsAttribute : Attribute
    {
        #region Public Properties

        /// <summary>Gets the explicit replacement, or null to use configured JsonMaskValue.</summary>
        public string? Replacement { get; }

        #endregion

        #region Constructors

        /// <summary>Leaves replacement selection to the active configuration.</summary>
        protected MaskInLogsAttribute() { }

        /// <summary>Stores a non-null literal replacement; an empty string is valid.</summary>
        /// <param name="replacement">Text that replaces the entire captured value.</param>
        /// <exception cref="ArgumentNullException">The supplied replacement is null.</exception>
        protected MaskInLogsAttribute(string replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            Replacement = replacement;
        }

        #endregion
    }
}
