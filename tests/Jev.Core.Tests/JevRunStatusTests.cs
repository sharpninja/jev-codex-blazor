using Jev.Core.Runtime;
using Jev.Workers;

namespace Jev.Core.Tests;

public sealed class JevRunStatusTests
{
    [Fact]
    public void Idle_and_Thinking_clear_worker_progress_from_the_previous_strategy()
    {
        var status = new JevRunStatus();
        status.SetActiveStrategy("Grok Build");
        status.ReportWorker(new CodingProgress("starting", "Starting Grok Build in /tmp/workspace"));

        Assert.Single(status.WorkerEvents);
        Assert.Equal(JevPhase.RunningWorker, status.Phase);

        status.SetPhase(JevPhase.Idle, "New conversation");
        Assert.Empty(status.WorkerEvents);
        Assert.Equal(JevPhase.Idle, status.Phase);

        status.ReportWorker(new CodingProgress("starting", "Starting Cline"));
        status.SetPhase(JevPhase.Thinking, "Jev is planning");
        Assert.Empty(status.WorkerEvents);
        Assert.Equal(JevPhase.Thinking, status.Phase);
    }
}
