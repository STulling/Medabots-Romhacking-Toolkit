namespace Medabots.Rom.Projects;

public interface IProjectEditCollectionAdapter<TEdit, TKey>
{
    IList<TEdit> GetCollection(RomHackProject project);

    TKey GetKey(TEdit edit);
}
