using LogThis.Enums;
using LogThis.Extensions;
using MethodBoundaryAspect.Fody.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LogThis.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class LogThisEndpointAttribute() : TypeFilterAttribute(typeof(LogThisEndpointAttributeFilter))
    {
        #region Implemenation Class

        private sealed class LogThisEndpointAttributeFilter(ILoggerFactory loggerFactory) : OnMethodBoundaryAspect, IActionFilter, IResultFilter
        {
            #region Private Properties

            private readonly ILogger _logger = loggerFactory.CreateLogger<LogThisEndpointAttribute>();

            private static JsonSerializerSettings SerializeSettings => new()
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            #endregion

            #region Private Methods

            private static string ExtractRequestJson(HttpContext httpContext)
            {
                HttpRequest httpRequest = httpContext.Request;

                httpRequest.EnableBuffering();
                httpRequest.Body.Seek(0, SeekOrigin.Begin);

                using StreamReader reader = new(httpRequest.Body);
                string rawBodyContent = reader.ReadToEndAsync().Result;

                dynamic? bodyContent = JsonConvert.DeserializeObject(rawBodyContent);

                httpRequest.Body.Position = 0;

                return bodyContent != null ? JsonConvert.SerializeObject(bodyContent, SerializeSettings) : string.Empty;
            }

            #endregion

            #region Public Methods

            public void OnActionExecuted(ActionExecutedContext context) { }

            public void OnActionExecuting(ActionExecutingContext context) 
            {
                string requestJson = ExtractRequestJson(context.HttpContext);

                _logger.LogEndpoint(LogStatus.Start, LogLevel.Information, context.HttpContext, requestJson);
            }
            public override void OnEntry(MethodExecutionArgs arg)
            {
                _logger.LogInformation($"Log This: {arg.Method.Name}");
            }

            public void OnResultExecuted(ResultExecutedContext context) { }

            public void OnResultExecuting(ResultExecutingContext context) { }

            #endregion
        }

        #endregion
    }
}
