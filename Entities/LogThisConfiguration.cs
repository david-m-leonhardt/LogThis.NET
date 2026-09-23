using LogThis.Constants;
using LogThis.Extensions;
using LogThis.Interfaces;

namespace LogThis.Entities
{
    public sealed class LogThisConfiguration() : ILogThisConfiguration
    {
        #region Public Properties

        public bool DebugLogThis { get; set; } = false;

        public List<string> JsonFieldsToMask { get; set; } = [];

        public string JsonMaskValue { get; set; } = MessageComponentConstants.DefaultJsonMaskValue;

        public bool LogClassName { get; set; } = false;

        public bool LogMethodArguments { get; set; } = false;

        public bool LogMethodName { get; set; } = true;

        public bool LogMethodReturnValue { get; set; }

        public Dictionary<string, object> MessageComponents { get; } = [];

        public string MessageDelimeter { get; set; } = MessageComponentConstants.DefaultDelimeter;

        public IAccessPointConfiguration OnEntryConfig { get; set; } = new OnEntryConfiguration();

        public IAccessPointConfiguration OnExceptionConfig { get; set; } = new OnExceptionConfiguration();

        public IAccessPointConfiguration OnExitConfig { get; set; } = new OnExitConfiguration();

        #endregion

        #region Public Methods

        public void AddMessageComponents(Dictionary<string, object> messageComponents)
        {
            foreach (var component in messageComponents)
            {
                MessageComponents.Add(component.Key.PrepComponentName(), component.Value);
            }
        }

        public List<IMessageComponentBuilder> GetComponentBuilders()
        {
            List<IMessageComponentBuilder> componentBuilders = [];

            AddAccessPointBuilder(OnEntryConfig);
            AddAccessPointBuilder(OnExceptionConfig);
            AddAccessPointBuilder(OnExitConfig);

            List<(bool Enabled, IMessageComponentBuilder Builder)> optionalBuilders =
            [
                (LogClassName, new ClassComponentBuilder()),
                (LogMethodName, new MethodComponentBuilder()),
                (LogMethodArguments, new ArgumentComponentBuilder()),
                (LogMethodReturnValue, new ReturnValueComponentBuilder())
            ];

            foreach ((bool enabled, IMessageComponentBuilder builder) in optionalBuilders)
            {
                if (enabled)
                {
                    componentBuilders.Add(builder);
                }
            }

            return componentBuilders;

            void AddAccessPointBuilder(IAccessPointConfiguration accessPoint)
            {
                if (accessPoint.LogAccessPoint && accessPoint is IMessageComponentBuilder builder)
                {
                    componentBuilders.Add(builder);
                }
            }
        }

        #endregion
    }
}
