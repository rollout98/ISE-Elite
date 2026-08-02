namespace ISE.Core.Engines;

/// <summary>
/// Defines a deterministic engine that transforms one immutable input into one immutable output.
/// </summary>
/// <typeparam name="TInput">The immutable input contract consumed by the engine.</typeparam>
/// <typeparam name="TOutput">The immutable output contract published by the engine.</typeparam>
public interface IEngine<in TInput, out TOutput>
{
    /// <summary>
    /// Processes the supplied input and returns the deterministic engine output.
    /// </summary>
    /// <param name="input">The validated immutable input contract.</param>
    /// <returns>The immutable result produced by the engine.</returns>
    TOutput Process(TInput input);
}
