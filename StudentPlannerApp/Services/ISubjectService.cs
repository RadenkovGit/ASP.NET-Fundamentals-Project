using StudentPlannerApp.Models;

namespace StudentPlannerApp.Services;

public interface ISubjectService
{
    Task<List<Subject>> GetAllAsync();
    Task<int> GetCountAsync();
}
