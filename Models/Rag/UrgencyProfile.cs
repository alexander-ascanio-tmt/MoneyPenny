using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models.Rag;

public class UrgencyProfile
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Instrucciones IA (respuesta)")]
    public string ResponseInstructions { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
