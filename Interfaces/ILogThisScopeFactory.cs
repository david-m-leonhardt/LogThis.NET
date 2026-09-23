using LogThis.Constants;

namespace LogThis.Interfaces;

public interface ILogThisScopeFactory
{
    IDisposable BeginScope(string categoryName = MessageComponentConstants.DefaultCategoryName);
}
