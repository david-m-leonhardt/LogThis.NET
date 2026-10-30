using LogThis.Attributes;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Original buffering reproduction, now a regular passing regression with capture disabled.</summary>
public static class IteratorBufferingChecks
{
    #region Public Methods

    /// <summary>Compares unannotated, method-level, and class-level lazy calls.</summary>
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogThisConfiguration(options =>
        {
            options.LogMethodArguments = false;
            options.LogMethodReturnValue = false;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        var failures = new List<string>();
        Probe("Unannotated", Baseline, failures);
        Probe("Method LogThis", new MethodIteratorProbe().Read, failures);
        Probe("Class LogThis", new ClassIteratorProbe().Read, failures);
        if (failures.Count != 0)
            throw new Exception(string.Join(Environment.NewLine, failures));
        Console.WriteLine("Iterator laziness checks passed.");
    }

    #endregion

    #region Private Methods

    /// <summary>Asserts that requesting a sequence does no body work and First produces only one item.</summary>
    private static void Probe(string name, Func<List<int>, IEnumerable<int>> read, List<string> failures)
    {
        var produced = new List<int>();
        IEnumerable<int> sequence = read(produced);
        int atCall = produced.Count;
        int first = sequence.First();
        Console.WriteLine($"{name}: produced at call={atCall}; after First()={produced.Count}; result={first}");
        if (atCall != 0 || !produced.SequenceEqual(new[] { 1 }) || first != 1)
            failures.Add($"{name}: expected no work at call and only item 1 after First().");
    }

    /// <summary>Provides the uninstrumented reference behavior.</summary>
    private static IEnumerable<int> Baseline(List<int> produced)
    {
        produced.Add(1);
        yield return 1;
        produced.Add(2);
        yield return 2;
    }

    #endregion
}

/// <summary>Method-level counterpart of the original buffering reproduction.</summary>
public sealed class MethodIteratorProbe
{
    #region Public Methods

    /// <summary>Records each item only when requested by the consumer.</summary>
    [LogThis]
    public IEnumerable<int> Read(List<int> produced)
    {
        produced.Add(1);
        yield return 1;
        produced.Add(2);
        yield return 2;
    }

    #endregion
}

/// <summary>Class-level counterpart of the original buffering reproduction.</summary>
[LogThis]
public sealed class ClassIteratorProbe
{
    #region Public Methods

    /// <summary>Records each item only when requested by the consumer.</summary>
    public IEnumerable<int> Read(List<int> produced)
    {
        produced.Add(1);
        yield return 1;
        produced.Add(2);
        yield return 2;
    }

    #endregion
}
