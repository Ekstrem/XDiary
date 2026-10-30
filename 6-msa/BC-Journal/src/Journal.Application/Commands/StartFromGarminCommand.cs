using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: начать запись из занятия Garmin.</summary>
public class StartFromGarminCommand : IRequest<JournalOperationResult>
{
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string GarminId { get; set; } = string.Empty;
}
