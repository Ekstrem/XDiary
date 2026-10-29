using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Application.Commands;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.InternalContracts;
using MediatR;

namespace Privacy.Application.Queries;

public class VerifyPinHandler : IRequestHandler<VerifyPinQuery, bool>
{
    private readonly IReadRepository<IPrivacy, PrivacyReadModel> _repository;

    public VerifyPinHandler(IReadRepository<IPrivacy, PrivacyReadModel> repository) => _repository = repository;

    public async Task<bool> Handle(VerifyPinQuery request, CancellationToken ct)
    {
        var id = PinCredentialModelFactory.ToGuid(request.UserId);
        var credential = await _repository.GetByIdAsync(id, ct);
        if (credential == null)
            return false;

        return PasswordHasher.Verify(request.Pin, credential.Salt, credential.Hash, credential.Iterations);
    }
}
