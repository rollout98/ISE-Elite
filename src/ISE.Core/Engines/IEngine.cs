namespace ISE.Core.Engines;

/// <summary>
/// Defines a deterministic engine that transforms one immutable input into one immutable output.
/// </summary>
public interface IEngine<in TInput, out TOutput>
{
    TOutput Process(TInput input);
}
