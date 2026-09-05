using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace methodlog
{
    public class LoggingPropertiesMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering();

            await _next(context);
        }
    }
}
  