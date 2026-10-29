using MediatR;

namespace Privacy.Application.Queries;

/// <summary>Запрос: установлен ли PIN у пользователя.</summary>
public class HasPinQuery : IRequest<bool>
{
    public string UserId { get; init; } = string.Empty;
}
