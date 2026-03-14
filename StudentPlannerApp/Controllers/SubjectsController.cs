using Microsoft.AspNetCore.Mvc;
using StudentPlannerApp.Services;

namespace StudentPlannerApp.Controllers;

public class SubjectsController : Controller
{
    private readonly ISubjectService subjectService;

    public SubjectsController(ISubjectService subjectService)
    {
        this.subjectService = subjectService;
    }

    public async Task<IActionResult> Index()
    {
        var subjects = await subjectService.GetAllAsync();
        return View(subjects);
    }
}
