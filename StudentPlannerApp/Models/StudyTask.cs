using System.ComponentModel.DataAnnotations;

namespace StudentPlannerApp.Models;

public class StudyTask
{
    public int Id { get; set; }

    [Required]
    [StringLength(80, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Due Date")]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; } = DateTime.Today.AddDays(1);

    [Display(Name = "Completed")]
    public bool IsCompleted { get; set; }

    public PriorityLevel Priority { get; set; }

    [Display(Name = "Subject")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a subject.")]
    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public ICollection<StudyNote> Notes { get; set; } = new List<StudyNote>();
}
