using System.ComponentModel.DataAnnotations;

namespace StudentPlannerApp.Models;

public class StudyNote
{
    public int Id { get; set; }

    [Required]
    [StringLength(250)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; } = DateTime.Now;

    public int StudyTaskId { get; set; }

    public StudyTask? StudyTask { get; set; }
}
