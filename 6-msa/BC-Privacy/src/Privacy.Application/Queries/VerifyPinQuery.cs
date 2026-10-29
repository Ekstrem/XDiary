using MediatR;

namespace Privacy.Application.Queries;

/// <summary>Запрос: проверить PIN пользователя.</summary>
public class VerifyPinQuery : IRequest<bool>
{
    public string UserId { get; init; } = string.Empty;
    public string Pin { get; init; } = string.Empty;
}
