using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class UrgencyRule
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La frase es obligatoria.")]
    [MaxLength(200)]
    [Display(Name = "Frase")]
    public string Phrase { get; set; } = string.Empty;

    [Display(Name = "Orden")]
    public int SortOrder { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
