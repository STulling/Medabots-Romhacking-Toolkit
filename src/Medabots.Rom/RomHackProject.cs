using Medabots.Rom.Text;

namespace Medabots.Rom;

public sealed class RomHackProject
{
    public string Name { get; set; } = "New Medabots Hack";

    public string? ProjectFilePath { get; set; }

    public string? SourceRomPath { get; set; }

    public string? TextProfileId { get; set; }

    public IList<RomPatchAction> PendingActions { get; } = new List<RomPatchAction>();

    public IList<MessagePatch> MessagePatches { get; } = new List<MessagePatch>();

    public IList<EventLabelPatch> EventLabels { get; } = new List<EventLabelPatch>();

    public IList<EventScriptPatch> EventScriptPatches { get; } = new List<EventScriptPatch>();
}
