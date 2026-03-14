using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudentPlannerApp.Data;
using StudentPlannerApp.Models;

namespace StudentPlannerApp.Services;

public class StudyTaskService : IStudyTaskService
{
    private readonly ApplicationDbContext dbContext;

    public StudyTaskService(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<List<StudyTask>> GetAllAsync()
    {
        return await dbContext.StudyTasks
            .Include(t => t.Subject)
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Deadline)
            .ToListAsync();
    }

    public async Task<StudyTask?> GetByIdAsync(int id)
    {
        return await dbContext.StudyTasks
            .Include(t => t.Subject)
            .Include(t => t.Notes.OrderByDescending(n => n.CreatedOn))
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task CreateAsync(StudyTask task)
    {
        dbContext.StudyTasks.Add(task);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(StudyTask task)
    {
        dbContext.StudyTasks.Update(task);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var task = await dbContext.StudyTasks.FindAsync(id);

        if (task != null)
        {
            dbContext.StudyTasks.Remove(task);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await dbContext.StudyTasks.AnyAsync(t => t.Id == id);
    }

    public async Task<List<SelectListItem>> GetSubjectOptionsAsync()
    {
        return await dbContext.Subjects
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name
            })
            .ToListAsync();
    }
}
