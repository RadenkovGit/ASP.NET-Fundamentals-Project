using Microsoft.EntityFrameworkCore;
using StudentPlannerApp.Models;

namespace StudentPlannerApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<StudyTask> StudyTasks { get; set; } = null!;
    public DbSet<Subject> Subjects { get; set; } = null!;
    public DbSet<StudyNote> StudyNotes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Subject>()
            .HasMany(s => s.StudyTasks)
            .WithOne(t => t.Subject)
            .HasForeignKey(t => t.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StudyTask>()
            .HasMany(t => t.Notes)
            .WithOne(n => n.StudyTask)
            .HasForeignKey(n => n.StudyTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
