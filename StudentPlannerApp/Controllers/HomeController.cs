using Microsoft.AspNetCore.Mvc;
using StudentPlannerApp.Services;
using StudentPlannerApp.ViewModels;

namespace StudentPlannerApp.Controllers;

public class HomeController : Controller
{
    private readonly IStudyTaskService studyTaskService;

    public HomeController(IStudyTaskService studyTaskService)
    {
        this.studyTaskService = studyTaskService;
    }

    public async Task<IActionResult> Index()
    {
        var allTasks = await studyTaskService.GetAllAsync();

        var model = new HomeIndexViewModel
        {
            TotalTasks = allTasks.Count,
            CompletedTasks = allTasks.Count(t => t.IsCompleted),
            UpcomingTasks = allTasks
                .Where(t => !t.IsCompleted)
                .Take(3)
                .ToList()
        };

        return View(model);
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
