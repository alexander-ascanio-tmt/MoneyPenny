using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class TicketIntent
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [MaxLength(50)]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Display(Name = "Orden")]
    public int SortOrder { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TicketIntentAssignment> Assignments { get; set; } = [];
}
