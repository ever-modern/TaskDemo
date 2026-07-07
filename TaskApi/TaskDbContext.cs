using Microsoft.EntityFrameworkCore;

namespace TasksDemo.Api;

public class TaskDbContext(DbContextOptions<TaskDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("task_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });
    }
}
