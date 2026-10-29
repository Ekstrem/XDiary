using MediatR;

namespace Privacy.Application.Commands;

/// <summary>Команда: удалить PIN.</summary>
public class RemovePinCommand : IRequest<PrivacyOperationResult>
{
    public string UserId { get; set; } = string.Empty;
}
