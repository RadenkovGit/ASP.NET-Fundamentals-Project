using Microsoft.AspNetCore.Mvc.Rendering;
using StudentPlannerApp.Models;

namespace StudentPlannerApp.Services;

public interface IStudyTaskService
{
    Task<List<StudyTask>> GetAllAsync();
    Task<StudyTask?> GetByIdAsync(int id);
    Task CreateAsync(StudyTask task);
    Task UpdateAsync(StudyTask task);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<List<SelectListItem>> GetSubjectOptionsAsync();
}
