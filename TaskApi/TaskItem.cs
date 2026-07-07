using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TasksDemo.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Priority
{
    Low,
    Medium,
    High
}

public class TaskItem
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
