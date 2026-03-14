using System.ComponentModel.DataAnnotations;

namespace StudentPlannerApp.Models;

public class Subject
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string Lecturer { get; set; } = string.Empty;

    public ICollection<StudyTask> StudyTasks { get; set; } = new List<StudyTask>();
}
