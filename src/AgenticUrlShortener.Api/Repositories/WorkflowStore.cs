using System.Collections.Concurrent;
using AgenticUrlShortener.Api.Models;

namespace AgenticUrlShortener.Api.Repositories;

public interface IWorkflowStore
{
    void Save(WorkflowRun run);
    WorkflowRun? Get(Guid id);
    IReadOnlyCollection<WorkflowRun> GetAll();
}

public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowRun> _runs = new();

    public void Save(WorkflowRun run) => _runs[run.Id] = run;

    public WorkflowRun? Get(Guid id)
    {
        _runs.TryGetValue(id, out var run);
        return run;
    }

    public IReadOnlyCollection<WorkflowRun> GetAll() =>
        _runs.Values.ToArray();
}
