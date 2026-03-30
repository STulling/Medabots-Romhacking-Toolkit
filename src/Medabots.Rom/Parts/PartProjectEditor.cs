using Medabots.Rom.Projects;

namespace Medabots.Rom.Parts;

public sealed class PartProjectEditor
{
    public PartDefinition? StagePart(RomHackProject project, PartDefinition sourcePart, PartDefinition editedPart)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourcePart);
        ArgumentNullException.ThrowIfNull(editedPart);

        if (PartTableReader.Serialize(sourcePart).SequenceEqual(PartTableReader.Serialize(editedPart)))
        {
            ProjectEditCollection.Remove(project, ProjectEditAdapters.Part, sourcePart.Id);
            return null;
        }

        ProjectEditCollection.Upsert(project, ProjectEditAdapters.Part, editedPart);
        return editedPart;
    }
}
