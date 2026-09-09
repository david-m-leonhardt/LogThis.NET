using LogThis.Constants;
using LogThis.Extensions;
using LogThis.Interfaces;

namespace LogThis.Entities
{
    public sealed class LogThisConfiguration() : ILogThisConfiguration
    {
        List<(bool, IMessageComponentBuilder)> ComponentBuilders =>
        [
            (OnEntryConfig.LogAccessPoint, OnEntryConfig as IMessageComponentBuilder),
            (OnExceptionConfig.LogAccessPoint, OnExceptionConfig as IMessageComponentBuilder),
            (OnExitConfig.LogAccessPoint, OnExitConfig as IMessageComponentBuilder),
            (LogClassName, new ClassComponentBuilder()),
            (LogMethodName, new MethodComponentBuilder()),
            (LogMethodArguments, new ArgumentComponentBuilder()),
            (LogMethodReturnValue, new ReturnValueComponentBuilder())
        ];

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

            ComponentBuilders.ForEach(builder =>
            {
                if (builder.Item1)
                {
                    componentBuilders.Add(builder.Item2);
                }
            });

            return componentBuilders;
        }

        #endregion
    }
}
