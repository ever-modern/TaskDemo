using Microsoft.EntityFrameworkCore;
using TasksDemo.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddSingleton<ITasksQueue, RabbitMqTasksQueue>();

var app = builder.Build();

var tasksApi = app.MapGroup("/tasks");

tasksApi.MapPost(
    "/",
    async (CreateTaskRequest request, TaskDbContext db) =>
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });
        if (request.Title.Length > 200)
            return Results.BadRequest(new { error = "Title must be at most 200 characters" });

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            Priority = request.Priority,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        return Results.Created($"/tasks/{task.Id}", task);
    }
);

tasksApi.MapGet(
    "/",
    async (TaskDbContext db) =>
    {
        var tasks = await db.Tasks.OrderBy(t => t.CreatedAt).ToListAsync();
        return Results.Ok(tasks);
    }
);

tasksApi.MapPut(
    "/{id:guid}/complete",
    async (Guid id, TaskDbContext db, ITasksQueue rabbit) =>
    {
        var task = await db.Tasks.FindAsync(id);
        if (task is null)
            return Results.NotFound(new { error = "Task not found" });
        if (task.IsCompleted)
            return Results.Conflict(new { error = "Task is already completed" });

        task.IsCompleted = true;
        task.CompletedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Task was modified by another request" });
        }

        try
        {
            await rabbit.PublishTaskCompletedAsync(task);
        }
        catch { }

        return Results.Ok(task);
    }
);

tasksApi.MapDelete(
    "/{id:guid}",
    async (Guid id, TaskDbContext db) =>
    {
        var task = await db.Tasks.FindAsync(id);
        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
);

app.Run();

public partial class Program { }
