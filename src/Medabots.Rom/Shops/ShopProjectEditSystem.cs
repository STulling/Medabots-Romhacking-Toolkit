using Medabots.Rom.Projects;

namespace Medabots.Rom.Shops;

internal sealed class ShopProjectEditSystem : IProjectEditSystem
{
    private readonly ShopPatcher _patcher;

    public ShopProjectEditSystem(ShopPatcher patcher)
    {
        _patcher = patcher;
    }

    public string DisplayName => "Shop";

    public IEnumerable<string> DescribeChanges(RomHackProject project) =>
        project.ShopEdits.Select(edit => $"Shop {edit.Id:D3}");

    public IEnumerable<ProjectChange> BuildChanges(RomHackProject project, ProjectBuildContext context)
    {
        return project.ShopEdits
            .OrderBy(edit => edit.Id)
            .Select(shop => new ProjectChange(DisplayName, $"Shop {shop.Id} patch", [_patcher.BuildPatch(shop)]))
            .ToArray();
    }
}
