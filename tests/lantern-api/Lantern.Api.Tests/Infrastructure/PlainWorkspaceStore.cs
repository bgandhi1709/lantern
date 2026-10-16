using Azure.Storage.Blobs;
using Lantern.Core.Constants;
using Lantern.Repository;

namespace Lantern.Api.Tests.Infrastructure;

// A store for tests that only create or remove a Workspace: it has no Board master, so it cannot copy a Content slice.
internal static class PlainWorkspaceStore
{
    public static WorkspaceStore Over(BlobContainerClient container, bool createContainer) =>
        new(container, new Dictionary<BoardType, BlobContainerClient>(), TimeProvider.System, TestServices.Json, TestServices.Mapper, TestServices.Layout, createContainer);
}
