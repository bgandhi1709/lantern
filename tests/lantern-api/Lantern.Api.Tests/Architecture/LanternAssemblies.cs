extern alias Functions;

using System.Reflection;
using Lantern.Base;
using Lantern.Core;
using Lantern.Repository;
using FunctionsModule = Functions::Lantern.Functions.FunctionsModule;

namespace Lantern.Api.Tests.Architecture;

// The production assemblies the architecture rules apply to, by layer.
internal static class LanternAssemblies
{
    public static readonly Assembly Core = typeof(CoreModule).Assembly;
    public static readonly Assembly Repository = typeof(RepositoryModule).Assembly;
    public static readonly Assembly Base = typeof(BaseModule).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;
    public static readonly Assembly Functions = typeof(FunctionsModule).Assembly;

    public static readonly Assembly[] All = [Core, Repository, Base, Api, Functions];

    public static IEnumerable<Type> Types =>
        All.SelectMany(assembly => assembly.GetTypes()).Where(type => !type.Name.Contains('<', StringComparison.Ordinal));

    public static IEnumerable<Type> Concrete => Types.Where(type => type is { IsClass: true, IsAbstract: false });

    /// <summary>The repository root, found from the test's output folder under dist/.</summary>
    public static DirectoryInfo RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Lantern.slnx")))
            {
                directory = directory.Parent;
            }

            return directory ?? throw new InvalidOperationException("Lantern.slnx not found above the test output.");
        }
    }
}
