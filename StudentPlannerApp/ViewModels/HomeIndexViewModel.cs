using StudentPlannerApp.Models;

namespace StudentPlannerApp.ViewModels;

public class HomeIndexViewModel
{
    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public List<StudyTask> UpcomingTasks { get; set; } = new();
}
