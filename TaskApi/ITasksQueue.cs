namespace TasksDemo.Api;

public interface ITasksQueue
{
    Task PublishTaskCompletedAsync(TaskItem task, CancellationToken cancellationToken = default);
}
