using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Entities
{
    public abstract class MessageComponentBase : IMessageComponentBuilder
    {
        #region Public Properties

        public abstract string Message { get; }

        #endregion

        #region Public Methods

        public abstract object BuildArg(MethodExecutionArgs arg);

        public virtual bool IncludeThisBuilder(IAccessPointConfiguration accessPointConfig)
        {
            return true;
        }

        #endregion
    }
}
