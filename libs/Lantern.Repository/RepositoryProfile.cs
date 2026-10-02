using Lantern.Core.Models;
using Lantern.Repository.Entities;
using Mapster;

namespace Lantern.Repository;

// Service model <-> entity. Encrypted columns carry plaintext here: the repository encrypts after mapping to an
// entity and decrypts before mapping back.
internal sealed class RepositoryProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.NewConfig<Child, ChildEntity>().Map(entity => entity.NameCipher, child => child.Name).Map(entity => entity.SchoolCipher, child => child.School);
        config.NewConfig<ChildEntity, Child>().Map(child => child.Name, entity => entity.NameCipher).Map(child => child.School, entity => entity.SchoolCipher);

        config.NewConfig<Parent, ParentEntity>().Map(entity => entity.NameCipher, parent => parent.Name).Map(entity => entity.EmailCipher, parent => parent.Email);
        config.NewConfig<ParentEntity, Parent>().Map(parent => parent.Name, entity => entity.NameCipher).Map(parent => parent.Email, entity => entity.EmailCipher);
    }
}
