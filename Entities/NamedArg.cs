using System.Runtime.CompilerServices;

namespace LogThis.Entities
{
    public readonly struct NamedArg(object value, [CallerArgumentExpression("value")] string name = "")
    {
        #region Public Properties

        public string Name { get; } = name;

        public object Value { get; } = value;

        #endregion
    }
}
