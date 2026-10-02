using Lantern.Core.Models;
using Lantern.Core.Service;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Controllers;

// A controller only maps API models to service models and back: the service scopes every call to the caller's Family.
[ApiController]
[Authorize]
public abstract class ResourceControllerBase<TModel, TServiceModel>(IServiceBase<TServiceModel> service, IMapper mapper)
    : ControllerBase
    where TServiceModel : class, IFamilyModel
{
    protected IServiceBase<TServiceModel> Service => service;

    protected IMapper Mapper => mapper;

    protected TModel ToModel(TServiceModel serviceModel) => mapper.Map<TModel>(serviceModel);
}
