using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Application.Commands;
using Privacy.Domain;
using Privacy.InternalContracts;
using MediatR;

namespace Privacy.Application.Queries;

public class HasPinHandler : IRequestHandler<HasPinQuery, bool>
{
    private readonly IReadRepository<IPrivacy, PrivacyReadModel> _repository;

    public HasPinHandler(IReadRepository<IPrivacy, PrivacyReadModel> repository) => _repository = repository;

    public async Task<bool> Handle(HasPinQuery request, CancellationToken ct)
    {
        var id = PinCredentialModelFactory.ToGuid(request.UserId);
        return await _repository.GetByIdAsync(id, ct) != null;
    }
}
