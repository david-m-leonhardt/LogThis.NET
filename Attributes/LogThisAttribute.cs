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

        private static JsonSerializerSettings SerializerSettings => new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        #endregion

        #region Private Methods

        private static LogParameters BuildLogParameters(LogThisRuntime runtime, IAccessPointConfiguration accessPointConfig, MethodExecutionArgs arg)
        {
            LogParameters logParameters = new(runtime.Configuration.MessageDelimeter);

            foreach (IMessageComponentBuilder builder in runtime.ComponentBuilders)
            {
                if (builder.IncludeThisBuilder(accessPointConfig))
                {
                    logParameters.AddMessage(builder.Message);
                    logParameters.AddArgs(builder.BuildArg(arg));
                }
            }

            logParameters.AddMessageComponents(runtime.Configuration.MessageComponents);

            return logParameters;
        }

        private static void LogMessage(LogThisRuntime runtime, IAccessPointConfiguration accessPointConfig, MethodExecutionArgs arg)
        {
            if (!accessPointConfig.LogAccessPoint) return;

            try
            {
                LogParameters logParameters = BuildLogParameters(runtime, accessPointConfig, arg);

                if (ReferenceEquals(accessPointConfig, runtime.Configuration.OnExceptionConfig))
                {
                    runtime.Logger.Log(accessPointConfig.LogLevel, arg.Exception, logParameters.Message, logParameters.Args);
                }
                else
                {
                    runtime.Logger.Log(accessPointConfig.LogLevel, logParameters.Message, logParameters.Args);
                }
            }
            catch (Exception e)
            {
                if (runtime.Configuration.DebugLogThis)
                {
                    runtime.Logger.LogDebug(e, "LogThis failed while constructing a log message.");
                }
            }
        }

        private static string MaskJson(object obj)
        {
            string jsonContent = obj != null ? JsonConvert.SerializeObject(obj, SerializerSettings) : string.Empty;

            if (jsonContent.IsValidJson())
            {
                LogThisRuntime runtime = LogThisRuntimeContext.Current
                    ?? throw new InvalidOperationException("No LogThis scope is active.");
                string[] blackList = [.. runtime.Configuration.JsonFieldsToMask];
                string mask = runtime.Configuration.JsonMaskValue;

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
            LogThisRuntime? runtime = LogThisRuntimeContext.Current;
            if (runtime != null) LogMessage(runtime, runtime.Configuration.OnEntryConfig, arg);
        }

        public override void OnException(MethodExecutionArgs arg)
        {
            LogThisRuntime? runtime = LogThisRuntimeContext.Current;
            if (runtime != null) LogMessage(runtime, runtime.Configuration.OnExceptionConfig, arg);
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            LogThisRuntime? runtime = LogThisRuntimeContext.Current;
            if (runtime != null) LogMessage(runtime, runtime.Configuration.OnExitConfig, arg);
        }

        #endregion
    }
}
