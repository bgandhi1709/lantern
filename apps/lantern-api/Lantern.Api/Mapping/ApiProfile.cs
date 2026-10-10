using Lantern.Api.Models;
using Lantern.Core.Models;
using Mapster;

namespace Lantern.Api.Mapping;

// API model <-> service model. Only what is mapped to an API model ever reaches the app.
internal sealed class ApiProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config
            .NewConfig<FamilyRegisterRequest, Registration>()
            .Map(registration => registration.ConsentAccepted, request => request.Consent.Accepted)
            .Map(registration => registration.ConsentNoticeVersion, request => request.Consent.NoticeVersion);
    }
}
