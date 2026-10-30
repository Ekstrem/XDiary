using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: начать ручную запись (пустой черновик).</summary>
public class StartManualEntryCommand : IRequest<JournalOperationResult>
{
}
