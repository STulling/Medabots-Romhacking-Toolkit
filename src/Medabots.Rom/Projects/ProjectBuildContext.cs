using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed class ProjectBuildContext
{
    public ProjectBuildContext(RomFile sourceRom, ResolvedRomLayout layout)
    {
        SourceRom = sourceRom ?? throw new ArgumentNullException(nameof(sourceRom));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Allocator = new FreeSpaceAllocator(FreeSpaceAllocator.AlignUp(Math.Max(sourceRom.Length, 0x800000), 4));
    }

    public RomFile SourceRom { get; }

    public ResolvedRomLayout Layout { get; }

    public FreeSpaceAllocator Allocator { get; }
}
