using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class RagPromptSelectionRule
{
    public int Id { get; set; }

    public int PromptTemplateId { get; set; }

    [MaxLength(100)]
    [Display(Name = "Nombre")]
    public string? Name { get; set; }

    [Display(Name = "Urgente")]
    public bool? IsUrgent { get; set; }

    [MaxLength(50)]
    [Display(Name = "Código de intención")]
    public string? IntentCode { get; set; }

    [Display(Name = "Prioridad")]
    public int Priority { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public RagPromptTemplate PromptTemplate { get; set; } = null!;
}
