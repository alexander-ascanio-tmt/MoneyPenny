using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class UrgencyDetectionConfig
{
    public int Id { get; set; }

    [Display(Name = "Instrucciones para la IA")]
    public string PromptText { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
