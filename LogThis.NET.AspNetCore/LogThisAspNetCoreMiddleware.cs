using LogThis.Constants;
using LogThis.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace LogThis.Middleware;

/// <summary>Opens an independent LogThis scope for each ASP.NET Core request.</summary>
public static class LogThisAspNetCoreMiddleware
{
    #region Public Methods

    extension(WebApplication app)
    {
        /// <summary>Adds HTTP scope middleware using the default logger category.</summary>
        public void UseLogThis()
        {
            UseLogThis(app, MessageComponentConstants.DefaultCategoryName);
        }

        /// <summary>Opens an independent ambient scope around each ASP.NET Core request.</summary>
        /// <param name="categoryName">Logger category assigned to request events.</param>
        /// <exception cref="InvalidOperationException">LogThis services have not been registered.</exception>
        public void UseLogThis(string categoryName)
        {
            ILogThisScopeFactory scopeFactory = app.Services.GetRequiredService<ILogThisScopeFactory>();
            app.Use(async (_, next) =>
            {
                using IDisposable scope = scopeFactory.BeginScope(categoryName);
                await next();
            });
        }
    }

    #endregion
}
