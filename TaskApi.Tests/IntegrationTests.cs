using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TasksDemo.Api;
using Xunit;

namespace TasksDemo.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    readonly WebApplicationFactory<Program> _factory;
    readonly MockRabbitMqService _rabbitSpy;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _rabbitSpy = new MockRabbitMqService();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:UseInMemory", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITasksQueue>();
                services.AddSingleton<ITasksQueue>(_rabbitSpy);
            });
        });
    }

    public Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        db.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _rabbitSpy.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateAndCompleteTask_ShouldPublishRabbitMqEvent()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new
        {
            title = "Test task from integration test",
            priority = "High"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);
        Assert.Equal("Test task from integration test", created.Title);
        Assert.False(created.IsCompleted);
        Assert.Null(created.CompletedAt);

        var completeResponse = await client.PutAsync($"/tasks/{created.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();
        var completed = await completeResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
        Assert.NotNull(completed.CompletedAt);

        var published = _rabbitSpy.LastPublished;
        Assert.NotNull(published);
        Assert.Equal(created.Id, published.Value.TaskId);
        Assert.Equal("Test task from integration test", published.Value.Title);
        Assert.Equal("High", published.Value.Priority);
        Assert.NotNull(published.Value.CompletedAt);
        Assert.Equal(completed.CompletedAt, published.Value.CompletedAt);
    }

    [Fact]
    public async Task CompleteAlreadyCompletedTask_Returns409()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new { title = "Already done" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);

        var first = await client.PutAsync($"/tasks/{created.Id}/complete", null);
        first.EnsureSuccessStatusCode();

        var second = await client.PutAsync($"/tasks/{created.Id}/complete", null);
        Assert.Equal(StatusCodes.Status409Conflict, (int)second.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithEmptyTitle_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/tasks", new { title = "   " });
        Assert.Equal(StatusCodes.Status400BadRequest, (int)response.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_RemovesTaskSuccessfully()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new { title = "Task to delete" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/tasks/{created.Id}");
        Assert.Equal(StatusCodes.Status204NoContent, (int)deleteResponse.StatusCode);

        var getResponse = await client.GetAsync("/tasks");
        getResponse.EnsureSuccessStatusCode();
        var tasks = await getResponse.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(tasks);
        Assert.DoesNotContain(tasks, t => t.Id == created.Id);
    }
}

public class MockRabbitMqService : ITasksQueue
{
    readonly List<TaskCompletedEvent> _events = [];

    public TaskCompletedEvent? LastPublished => _events.LastOrDefault();
    public IReadOnlyList<TaskCompletedEvent> PublishedEvents => _events.AsReadOnly();

    public Task PublishTaskCompletedAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _events.Add(new TaskCompletedEvent(
            TaskId: task.Id,
            Title: task.Title,
            CompletedAt: task.CompletedAt,
            Priority: task.Priority.ToString()
        ));
        return Task.CompletedTask;
    }

    public void Reset() => _events.Clear();
}

public readonly record struct TaskCompletedEvent(
    Guid TaskId,
    string Title,
    DateTimeOffset? CompletedAt,
    string Priority
);
