using Microsoft.EntityFrameworkCore;
using StudentPlannerApp.Data;
using StudentPlannerApp.Models;

namespace StudentPlannerApp.Services;

public class SubjectService : ISubjectService
{
    private readonly ApplicationDbContext dbContext;

    public SubjectService(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<List<Subject>> GetAllAsync()
    {
        return await dbContext.Subjects
            .Include(s => s.StudyTasks)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await dbContext.Subjects.CountAsync();
    }
}
