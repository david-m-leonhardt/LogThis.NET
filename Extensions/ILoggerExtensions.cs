using JsonMasking;
using LogThis.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LogThis.Extensions
{
    internal static class ILoggerExtensions
    {
        #region Private Methods

        private static string BuildMessage(string jsonContent, params string[] messageElements)
        {
            return $"{string.Join(", ", messageElements)} {MaskJson(jsonContent)}";
        }

        private static string MaskJson(string jsonContent)
        {
            if(string.IsNullOrEmpty(jsonContent))
            {
                return string.Empty;
            }

            string[] blackList = [];
            string mask = "|";

            return jsonContent.MaskFields(blackList, mask);
        }

        #endregion

        #region Public Methods

        internal static void LogEndpoint(this ILogger logger, LogStatus logStatus, LogLevel logLevel, HttpContext httpContext, string jsonContent)
        {
            string message = BuildMessage(jsonContent, logStatus.ToString(), httpContext.Request.Path.Value.Replace("/", string.Empty) ?? "/");

            logger.LogThis(logLevel, message);
        }

        internal static void LogThis(this ILogger logger, LogLevel logLevel, string message)
        {
            logger.Log(logLevel, message);
        }

        #endregion
    }
}
