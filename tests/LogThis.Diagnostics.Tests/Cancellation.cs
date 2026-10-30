using System.Runtime.CompilerServices;
using LogThis.Attributes;

/// <summary>Consumer fixture for narrowly scoped cancellation-warning suppression.</summary>
public sealed class MethodCancellationDiagnostics
{
    #region Public Methods

    /// <summary>Valid method instrumentation must compile with CS8424 promoted to an error.</summary>
    [LogThis]
    public async IAsyncEnumerable<int> Valid([EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        yield return 1;
    }

#if INVALID_CANCELLATION
    /// <summary>Unrelated declarations must retain compiler diagnostics.</summary>
    public void Unrelated([EnumeratorCancellation] CancellationToken token) { } // CS8424

    /// <summary>Non-yield methods are not eligible for suppression.</summary>
    [LogThis]
    public void NonIterator([EnumeratorCancellation] CancellationToken token) { } // CS8424

    /// <summary>Explicitly excluded methods are not eligible for suppression.</summary>
    [LogThis, DontLogThis]
    public void Excluded([EnumeratorCancellation] CancellationToken token) { } // CS8424

    /// <summary>An annotated parameter of the wrong type must still be diagnosed.</summary>
    [LogThis]
    public async IAsyncEnumerable<int> WrongType([EnumeratorCancellation] int token) // CS8424
    {
        await Task.Yield();
        yield return token;
    }

#endif

    #endregion
}

/// <summary>Class advice must suppress only methods it actually instruments.</summary>
[LogThis]
public sealed class ClassCancellationDiagnostics
{
    #region Public Methods

    /// <summary>Valid class instrumentation must compile without the generated warning.</summary>
    public async IAsyncEnumerable<int> Valid([EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        yield return 1;
    }

#if INVALID_CANCELLATION
    /// <summary>An excluded class member retains invalid-usage warnings.</summary>
    [DontLogThis]
    public void Excluded([EnumeratorCancellation] CancellationToken token) { } // CS8424

    /// <summary>A non-yield class member retains invalid-usage warnings.</summary>
    public void NonIterator([EnumeratorCancellation] CancellationToken token) { } // CS8424

    /// <summary>Private methods are outside class-level instrumentation.</summary>
    private void Private([EnumeratorCancellation] CancellationToken token) { } // CS8424
#endif

    #endregion
}
