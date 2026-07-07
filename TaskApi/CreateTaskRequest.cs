using TasksDemo.Api;


public record CreateTaskRequest(string Title, Priority Priority = Priority.Medium);
