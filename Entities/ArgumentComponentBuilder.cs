using LogThis.Attributes;
using LogThis.Constants;
using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Entities
{
    public class ArgumentComponentBuilder : MessageComponentBase
    {
        #region Public Properties

        public override string Message => MessageComponentConstants.ArgumentsLabel;

        #endregion

        #region Public Methods

        public override object BuildArg(MethodExecutionArgs arg)
        {
            return $"Arguments: {LogThisAttribute.MaskObjects(arg.Arguments)}";
        }

        public override bool IncludeThisBuilder(IAccessPointConfiguration accessPointConfig)
        {
            return accessPointConfig is not OnExitConfiguration;
        }

        #endregion
    }
}
