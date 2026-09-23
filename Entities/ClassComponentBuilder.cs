using LogThis.Constants;
using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Entities
{
    public class ClassComponentBuilder : MessageComponentBase
    {
        #region Public Properties

        public override string Message => MessageComponentConstants.ClassLabel;

        #endregion

        #region Public Methods

        public override object BuildArg(MethodExecutionArgs arg)
        {
            return arg.Method.DeclaringType?.Name ?? string.Empty;
        }

        #endregion
    }
}
