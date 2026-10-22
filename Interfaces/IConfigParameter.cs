namespace LogThis.Interfaces
{
    /// <summary>Describes one optional structured component in a generated log event.</summary>
    internal interface IConfigParameter
    {
        #region Properties

        /// <summary>Gets the structured placeholder; presentation labels are added by LogParameters.</summary>
        string Message { get; }

        /// <summary>Gets or sets the structured value paired with the placeholder.</summary>
        object? Obj { get; }

        /// <summary>Gets or sets whether both the placeholder and its value should be emitted.</summary>
        bool ShouldAddValue { get; }

        #endregion
    }
}
