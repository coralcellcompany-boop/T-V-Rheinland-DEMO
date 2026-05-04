using TuvInspection.Contracts.Certificates;
using TuvInspection.Domain.Certificates;

namespace TuvInspection.Infrastructure.Certificates;

internal static class CertificateMappers
{
    public static CertificateTransitionDto ToDto(this CertificateStateTransition t) =>
        new(t.Id,
            (CertificateStateDto)t.FromState,
            (CertificateStateDto)t.ToState,
            t.ActorUserId, t.ActorRole, t.Comments, t.AtUtc);
}
