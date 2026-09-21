namespace Jev.Core.Simulation;

public sealed record JevSimulationTurn(bool Succeeded, string Text)
{
    public static JevSimulationTurn Ok(string text) => new(true, text);

    public static JevSimulationTurn Error(string text) => new(false, text);
}
