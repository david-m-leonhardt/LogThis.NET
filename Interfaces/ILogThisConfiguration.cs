namespace LogThis.Interfaces
{
    public interface ILogThisConfiguration
    {
        #region Properties

        bool DebugLogThis { get; set; }

        List<string> JsonFieldsToMask { get; set; }

        string JsonMaskValue { get; set; }

        bool LogClassName { get; set; }

        bool LogMethodArguments { get; set; }

        bool LogMethodName { get; set; }

        bool LogMethodReturnValue { get; set; }

        Dictionary<string, object> MessageComponents { get; }

        string MessageDelimeter { get; set; }

        IAccessPointConfiguration OnEntryConfig { get; set; }

        IAccessPointConfiguration OnExceptionConfig { get; set; }

        IAccessPointConfiguration OnExitConfig { get; set; }

        #endregion

        #region Methods

        void AddMessageComponents(Dictionary<string, object> messageComponents);

        List<IMessageComponentBuilder> GetComponentBuilders();

        #endregion
    }
}
