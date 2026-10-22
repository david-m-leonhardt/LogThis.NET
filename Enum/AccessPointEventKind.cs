namespace LogThis.Enum
{
    /// <summary>Identifies the method boundary represented by a logging event.</summary>
    /// <remarks>
    /// Built-in configurations report the matching event kind.
    /// Custom implementations are responsible for returning the kind
    /// appropriate to where they are assigned.
    /// </remarks>
    public enum AccessPointEventKind
    {
        /// <summary>The method is about to execute.</summary>
        Entry,
        /// <summary>The method failed with an exception.</summary>
        Exception,
        /// <summary>The method completed successfully.</summary>
        Exit
    }
}
