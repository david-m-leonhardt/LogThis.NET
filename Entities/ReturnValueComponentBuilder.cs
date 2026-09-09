using LogThis.Attributes;
using LogThis.Constants;
using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Entities
{
    public class ReturnValueComponentBuilder : MessageComponentBase
    {
        #region Public Properties

        public override string Message => MessageComponentConstants.ReturnValueLabel;

        #endregion

        #region Public Methods

        public override object BuildArg(MethodExecutionArgs arg)
        {
            return $"ReturnValue: {LogThisAttribute.MaskObject(arg.ReturnValue)}";
        }

        public override bool IncludeThisBuilder(IAccessPointConfiguration accessPointConfig)
        {
            return accessPointConfig is not OnEntryConfiguration;
        }

        #endregion
    }
}
