using MethodBoundaryAspect.Fody.Attributes;

namespace LogThis.Interfaces
{
    public interface IMessageComponentBuilder
    {
        #region Properties

        string Message { get; }

        #endregion

        #region Methods

        object BuildArg(MethodExecutionArgs arg);

        bool IncludeThisBuilder(IAccessPointConfiguration accessPointConfig);

        #endregion
    }
}
