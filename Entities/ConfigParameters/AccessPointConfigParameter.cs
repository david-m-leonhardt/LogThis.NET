using LogThis.Constants;
using LogThis.Enum;
using LogThis.Interfaces;

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
        /// <param name="accessPoint">Event kind and configured event message.</param>
        /// <param name="runtime">Selected runtime configuration and operation state.</param>
        public AccessPointConfigParameter(IAccessPointConfiguration accessPoint, LogThisRuntime runtime) : base(runtime)
        {
            _message = EventKingLabelMap[accessPoint.EventKind];
            Obj = accessPoint.AccessPointMessage;
            ShouldAddValue = true;
        }

        #endregion
    }
}
