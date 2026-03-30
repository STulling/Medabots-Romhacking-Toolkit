namespace Medabots.Rom.Projects;

public static class ProjectEditCollection
{
    public static TEdit? Find<TEdit, TKey>(RomHackProject project, IProjectEditCollectionAdapter<TEdit, TKey> adapter, TKey key)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(adapter);

        var collection = adapter.GetCollection(project);
        var comparer = EqualityComparer<TKey>.Default;
        foreach (var edit in collection)
        {
            if (comparer.Equals(adapter.GetKey(edit), key))
            {
                return edit;
            }
        }

        return default;
    }

    public static void Upsert<TEdit, TKey>(RomHackProject project, IProjectEditCollectionAdapter<TEdit, TKey> adapter, TEdit edit)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(adapter);

        var collection = adapter.GetCollection(project);
        var key = adapter.GetKey(edit);
        var comparer = EqualityComparer<TKey>.Default;
        for (var index = 0; index < collection.Count; index++)
        {
            if (comparer.Equals(adapter.GetKey(collection[index]), key))
            {
                collection[index] = edit;
                return;
            }
        }

        collection.Add(edit);
    }

    public static bool Remove<TEdit, TKey>(RomHackProject project, IProjectEditCollectionAdapter<TEdit, TKey> adapter, TKey key)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(adapter);

        var collection = adapter.GetCollection(project);
        var comparer = EqualityComparer<TKey>.Default;
        for (var index = 0; index < collection.Count; index++)
        {
            if (comparer.Equals(adapter.GetKey(collection[index]), key))
            {
                collection.RemoveAt(index);
                return true;
            }
        }

        return false;
    }
}
