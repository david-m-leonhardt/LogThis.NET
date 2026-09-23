using LogThis.Constants;

namespace LogThis.Interfaces;

/// <summary>Starts a logging scope for an operation outside HTTP middleware.</summary>
public interface ILogThisScopeFactory
{
    /// <summary>Activates LogThis for the current asynchronous flow until disposed.</summary>
    /// <param name="categoryName">Logging category assigned to events in this scope.</param>
    /// <returns>A scope that restores the previous runtime when disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="categoryName"/> is empty or whitespace.</exception>
    IDisposable BeginScope(string categoryName = MessageComponentConstants.DefaultCategoryName);
}
