using LogThis.Constants;
using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Entities
{
    public class MethodComponentBuilder : MessageComponentBase
    {
        #region Public Properties

        public override string Message => MessageComponentConstants.MethodNameLabel;

        #endregion

        #region Public Properties

        public override object BuildArg(MethodExecutionArgs arg)
        {
            return arg.Method.Name;
        }

        #endregion
    }
}
