using Lantern.Core.Constants;
using Lantern.Core.Models;
using Lantern.Repository.Entities;
using Mapster;

namespace Lantern.Repository;

internal sealed class RepositoryProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.NewConfig<Family, FamilyEntity>().Map(entity => entity.Board, family => family.Board.ToString());
        config
            .NewConfig<FamilyEntity, Family>()
            .Map(family => family.Board, entity => string.IsNullOrEmpty(entity.Board) ? BoardType.Cbse : Enum.Parse<BoardType>(entity.Board));
        config.NewConfig<ClassSubject, Subject>().MapToConstructor(true);
    }
}
