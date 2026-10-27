using LogThis.Constants;
using LogThis.Enum;

namespace LogThis.Entities.ConfigParameters
{
    /// <summary>Supplies the configured event text and its entry, exit, or exception placeholder.</summary>
    internal sealed class AccessPointConfigParameter : ConfigParameter
    {
        #region Private Properties

        /// <summary>Maps event kinds to their library-owned structured placeholders.</summary>
        private Dictionary<AccessPointEventKind, string> EventKingLabelMap = new()
        {
            { AccessPointEventKind.Entry, MessageComponents.OnEntryLabel },
            { AccessPointEventKind.Exception, MessageComponents.OnExceptionLabel },
            { AccessPointEventKind.Exit, MessageComponents.OnExitLabel }
        };

        /// <summary>Structured placeholder chosen for this event.</summary>
        private string _message;

        #endregion

        #region Public Properties

        /// <inheritdoc />
        public override string Message => _message;

        #endregion

        #region Constructor

        /// <summary>Selects the placeholder for the configured event kind.</summary>
        /// <param name="runtimeValues">Event settings and selected runtime state used to choose the message placeholder.</param>
        public AccessPointConfigParameter(LogThisRuntimeValues runtimeValues) : base(runtimeValues)
        {
            _message = EventKingLabelMap[runtimeValues.AccessPoint.EventKind];
            Obj = runtimeValues.AccessPoint.AccessPointMessage;
            ShouldAddValue = true;
        }

        #endregion
    }
}
