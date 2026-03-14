using StudentPlannerApp.Models;

namespace StudentPlannerApp.Data;

public static class SeedData
{
    public static void Initialize(ApplicationDbContext dbContext)
    {
        if (!dbContext.Subjects.Any())
        {
            dbContext.Subjects.AddRange(
                new Subject { Name = "HTTP Protocol", Lecturer = "Mr. Petrov" },
                new Subject { Name = "ASP.NET and Databases", Lecturer = "Mrs. Ivanova" },
                new Subject { Name = "ASP.NET Core Identity", Lecturer = "Mr. Georgiev" }
            );

            dbContext.SaveChanges();
        }

        if (!dbContext.StudyTasks.Any())
        {
            var firstSubjectId = dbContext.Subjects.OrderBy(s => s.Id).First().Id;
            var secondSubjectId = dbContext.Subjects.OrderBy(s => s.Id).Skip(1).First().Id;

            dbContext.StudyTasks.AddRange(
                new StudyTask
                {
                    Title = "Review HTTP status codes",
                    Description = "Prepare short notes about request methods and common status codes.",
                    Deadline = DateTime.Today.AddDays(3),
                    IsCompleted = false,
                    Priority = PriorityLevel.High,
                    SubjectId = firstSubjectId
                },
                new StudyTask
                {
                    Title = "Prepare EF Core notes",
                    Description = "Collect examples for models, migrations and database relationships.",
                    Deadline = DateTime.Today.AddDays(5),
                    IsCompleted = false,
                    Priority = PriorityLevel.Medium,
                    SubjectId = secondSubjectId
                }
            );

            dbContext.SaveChanges();
        }

        if (!dbContext.StudyNotes.Any())
        {
            var firstTaskId = dbContext.StudyTasks.OrderBy(t => t.Id).First().Id;

            dbContext.StudyNotes.Add(new StudyNote
            {
                Content = "Remember to revise GET vs POST and model validation.",
                CreatedOn = DateTime.Now,
                StudyTaskId = firstTaskId
            });

            dbContext.SaveChanges();
        }
    }
}
