using JsonMasking;
using LogThis.Entities;
using LogThis.Extensions;
using LogThis.Interfaces;
using MethodBoundaryAspect.Fody.Attributes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LogThis.Attributes
{
    [AttributeUsage(validOn: AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LogThisAttribute : OnMethodBoundaryAspect
    {
        #region Private Properties

        private static List<IMessageComponentBuilder> componentBuilders;

        private static ILogThisConfiguration? config;

        private static ILogger? logger;

        private static JsonSerializerSettings SerializerSettings => new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        #endregion

        #region Private Methods

        private static LogParameters BuildLogParameters(IAccessPointConfiguration accessPointConfig, MethodExecutionArgs arg)
        {
            LogParameters logParameters = new(config.MessageDelimeter);

            foreach (IMessageComponentBuilder builder in componentBuilders)
            {
                if (builder.IncludeThisBuilder(accessPointConfig))
                {
                    logParameters.AddMessage(builder.Message);
                    logParameters.AddArgs(builder.BuildArg(arg));
                }
            }

            logParameters.AddMessageComponents(config.MessageComponents);

            return logParameters;
        }

        private static void LogMessage(IAccessPointConfiguration accessPointConfig, MethodExecutionArgs arg)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(config);

            try
            {
                LogParameters logParameters = BuildLogParameters(accessPointConfig, arg);

                if (accessPointConfig is OnExceptionConfiguration)
                {
                    logger?.Log(accessPointConfig.LogLevel, arg.Exception, logParameters.Message, logParameters.Args);
                }
                else
                {
                    logger?.Log(accessPointConfig.LogLevel, logParameters.Message, logParameters.Args);
                }
            }
            catch (Exception e)
            {
                if (config.DebugLogThis)
                {
                    logger?.LogDebug(e, e.Message, e.StackTrace);
                }
            }
        }

        private static string MaskJson(object obj)
        {
            string jsonContent = obj != null ? JsonConvert.SerializeObject(obj, SerializerSettings) : string.Empty;

            if (jsonContent.IsValidJson())
            {
                string[] blackList = [.. config.JsonFieldsToMask];
                string mask = config.JsonMaskValue;

                string maskedJsonContent = jsonContent.MaskFields(blackList, mask).Replace("\r\n", "");
                while (maskedJsonContent.Contains("  "))
                {
                    maskedJsonContent = maskedJsonContent.Replace("  ", " ");
                }

                return maskedJsonContent;
            }

            return jsonContent;
        }

        #endregion

        #region Public Methods  

        public static void Initialize(ILogger? logger, ILogThisConfiguration? logThisConfiguration)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(logThisConfiguration);

            LogThisAttribute.logger = logger;
            config = logThisConfiguration;
            componentBuilders = config.GetComponentBuilders();
        }

        public static string MaskObject(object obj)
        {
            string maskedString;

            if (obj is System.Collections.IList list)
            {
                object[] returnValues = new object[list.Count];
                list.CopyTo(returnValues, 0);

                maskedString = MaskObjects(returnValues);
            }
            else
            {
                maskedString = MaskJson(obj);
            }

            return maskedString;
        }

        public static string MaskObjects(object[] objects)
        {
            List<string> maskedStrings = [];

            foreach (object obj in objects)
            {
                maskedStrings.Add(MaskJson(obj));
            }

            return $"[{string.Join(", ", maskedStrings)}]";
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {
            LogMessage(config.OnEntryConfig, arg);
        }

        public override void OnException(MethodExecutionArgs arg)
        {
            LogMessage(config.OnExceptionConfig, arg);
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            LogMessage(config.OnExitConfig, arg);
        }

        #endregion
    }
}
  