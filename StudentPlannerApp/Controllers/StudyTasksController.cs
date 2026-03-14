using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StudentPlannerApp.Models;
using StudentPlannerApp.Services;

namespace StudentPlannerApp.Controllers;

public class StudyTasksController : Controller
{
    private readonly IStudyTaskService studyTaskService;

    public StudyTasksController(IStudyTaskService studyTaskService)
    {
        this.studyTaskService = studyTaskService;
    }

    public async Task<IActionResult> Index()
    {
        var tasks = await studyTaskService.GetAllAsync();
        return View(tasks);
    }

    public async Task<IActionResult> Details(int id)
    {
        var task = await studyTaskService.GetByIdAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        return View(task);
    }

    public async Task<IActionResult> Create()
    {
        await LoadSubjectsAsync();
        return View(new StudyTask());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudyTask task)
    {
        if (!ModelState.IsValid)
        {
            await LoadSubjectsAsync(task.SubjectId);
            return View(task);
        }

        await studyTaskService.CreateAsync(task);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var task = await studyTaskService.GetByIdAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        await LoadSubjectsAsync(task.SubjectId);
        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudyTask task)
    {
        if (id != task.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await LoadSubjectsAsync(task.SubjectId);
            return View(task);
        }

        if (!await studyTaskService.ExistsAsync(id))
        {
            return NotFound();
        }

        await studyTaskService.UpdateAsync(task);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var task = await studyTaskService.GetByIdAsync(id);

        if (task == null)
        {
            return NotFound();
        }

        return View(task);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await studyTaskService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadSubjectsAsync(int selectedSubjectId = 0)
    {
        var subjectOptions = await studyTaskService.GetSubjectOptionsAsync();

        ViewBag.Subjects = new SelectList(
            subjectOptions,
            nameof(SelectListItem.Value),
            nameof(SelectListItem.Text),
            selectedSubjectId.ToString());
    }
}
