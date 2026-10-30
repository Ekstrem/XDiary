namespace Journal.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using MediatR;

public sealed record OrgasmsDto(string Participant, int Count, IReadOnlyList<string> Types, int Rating);

public sealed record StartManualEntryCommand : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record StartFromGarminCommand(string StartedAt, int DurationMin, string GarminId) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record SpecifyWhenCommand(Guid AggregateId, string StartedAt) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record AddPartnerCommand(Guid AggregateId, string PartnerId) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record RemovePartnerCommand(Guid AggregateId, string PartnerId) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record SpecifyPlaceCommand(Guid AggregateId, string Place) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record AddPositionsCommand(Guid AggregateId, IReadOnlyList<string> Positions) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record AddAccessoriesCommand(Guid AggregateId, IReadOnlyList<string> Costumes, IReadOnlyList<string> Toys, IReadOnlyList<string> Fantasies, IReadOnlyList<string> Tags) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record RecordContraceptionCommand(Guid AggregateId, IReadOnlyList<string> Contraceptives, int ContraceptiveCount) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record SpecifyDurationCommand(Guid AggregateId, int DurationMin) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record RecordOrgasmsCommand(Guid AggregateId, IReadOnlyList<OrgasmsDto> Orgasms) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record CompleteCommand(Guid AggregateId) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;
public sealed record DeleteRecordCommand(Guid AggregateId) : IRequest<AggregateResult<IJournal, IJournalAnemicModel>>;