namespace Partners.Application.Commands;

/// <summary>
/// Черновик профиля партнёра (PartnerDraft из Command API).
/// </summary>
public class PartnerDraft
{
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = "female";
    public string Notes { get; set; } = string.Empty;
}
