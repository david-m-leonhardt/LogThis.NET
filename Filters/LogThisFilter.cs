using MethodBoundaryAspect.Fody.Attributes;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace LogThis.Filters
{
    public class LogThisFilter(ILogger logger) : OnMethodBoundaryAspect, IActionFilter, IResultFilter
    {
        #region Private Properties

        private readonly ILogger _logger = logger;

        public void OnActionExecuted(ActionExecutedContext context) { }

        public void OnActionExecuting(ActionExecutingContext context) { }

        #endregion

        public override void OnEntry(MethodExecutionArgs arg)
        {
            _logger.LogInformation($"Log This: {arg.Method.Name}");
        }

        public void OnResultExecuted(ResultExecutedContext context) { }

        public void OnResultExecuting(ResultExecutingContext context) { }
    }
}
