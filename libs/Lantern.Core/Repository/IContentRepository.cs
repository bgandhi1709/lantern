using Lantern.Core.Constants;
using Lantern.Core.Models;

namespace Lantern.Core.Repository;

public interface IContentRepository
{
    Task<ClassManifest> ClassAsync(BoardType board, int classLevel, CancellationToken cancellationToken);
}
