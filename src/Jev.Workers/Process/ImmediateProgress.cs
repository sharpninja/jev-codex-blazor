namespace Jev.Workers.Process;

/// <summary>
/// Invokes the handler on the reporting thread. <see cref="Progress{T}"/> can
/// post to a captured synchronization context and drop live CLI lines in tests.
/// </summary>
public sealed class ImmediateProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
