using MediatR;

namespace Privacy.Application.Commands;

/// <summary>Команда: установить PIN (открытый PIN не сохраняется).</summary>
public class SetPinCommand : IRequest<PrivacyOperationResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}
