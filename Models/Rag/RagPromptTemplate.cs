using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class RagPromptTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Prompt system")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Display(Name = "Plantilla user")]
    public string UserPromptTemplate { get; set; } = string.Empty;

    [Display(Name = "Pregunta de generación")]
    public string GenerationQuestion { get; set; } = string.Empty;

    [Display(Name = "Orden")]
    public int SortOrder { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RagPromptSelectionRule> SelectionRules { get; set; } = [];
}
