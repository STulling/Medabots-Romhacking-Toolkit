using System.Text.Json;
using System.Text.Json.Serialization;
using Medabots.Rom.Text;

namespace Medabots.Rom.Projects;

public static class RomHackProjectSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<RomHackProject> LoadAsync(string projectFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        await using var stream = File.OpenRead(projectFilePath);
        var document = await JsonSerializer.DeserializeAsync<RomHackProjectDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);

        if (document is null)
        {
            throw new InvalidDataException("The project file is empty or invalid.");
        }

        return document.ToProject(projectFilePath);
    }

    public static async Task SaveAsync(RomHackProject project, string projectFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var directory = Path.GetDirectoryName(projectFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(projectFilePath);
        await JsonSerializer.SerializeAsync(stream, RomHackProjectDocument.FromProject(project), SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed class RomHackProjectDocument
    {
        public int SchemaVersion { get; set; } = 3;

        public string Name { get; set; } = "New Medabots Hack";

        public string? SourceRomPath { get; set; }

        public string? TextProfileId { get; set; }

        public List<MessagePatchDocument> MessagePatches { get; set; } = [];

        public List<EventLabelPatchDocument> EventLabels { get; set; } = [];

        public List<EventScriptPatchDocument> EventScriptPatches { get; set; } = [];

        public RomHackProject ToProject(string? projectFilePath)
        {
            if (SchemaVersion is not 1 and not 2 and not 3)
            {
                throw new InvalidDataException($"Unsupported project schema version '{SchemaVersion}'.");
            }

            var project = new RomHackProject
            {
                Name = Name,
                SourceRomPath = SourceRomPath,
                ProjectFilePath = projectFilePath,
                TextProfileId = TextProfileId
            };

            foreach (var patch in MessagePatches)
            {
                project.MessagePatches.Add(new MessagePatch(new MessageId(patch.Bank, patch.Index), patch.Text));
            }

            foreach (var label in EventLabels)
            {
                project.EventLabels.Add(new EventLabelPatch((short)label.EventId, label.Offset, label.Label));
            }

            foreach (var patch in EventScriptPatches)
            {
                project.EventScriptPatches.Add(new EventScriptPatch((short)patch.EventId, Convert.FromBase64String(patch.ScriptBytesBase64)));
            }

            return project;
        }

        public static RomHackProjectDocument FromProject(RomHackProject project)
        {
            return new RomHackProjectDocument
            {
                Name = project.Name,
                SourceRomPath = project.SourceRomPath,
                TextProfileId = project.TextProfileId,
                MessagePatches = project.MessagePatches
                    .Select(patch => new MessagePatchDocument
                    {
                        Bank = patch.Id.Bank,
                        Index = patch.Id.Index,
                        Text = patch.Text
                    })
                    .ToList(),
                EventLabels = project.EventLabels
                    .Select(label => new EventLabelPatchDocument
                    {
                        EventId = label.EventId,
                        Offset = label.Offset,
                        Label = label.Label
                    })
                    .ToList(),
                EventScriptPatches = project.EventScriptPatches
                    .Select(patch => new EventScriptPatchDocument
                    {
                        EventId = patch.EventId,
                        ScriptBytesBase64 = Convert.ToBase64String(patch.ScriptBytes)
                    })
                    .ToList()
            };
        }
    }

    private sealed class MessagePatchDocument
    {
        public int Bank { get; set; }

        public int Index { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class EventLabelPatchDocument
    {
        public int EventId { get; set; }

        public int Offset { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class EventScriptPatchDocument
    {
        public int EventId { get; set; }

        public string ScriptBytesBase64 { get; set; } = string.Empty;
    }
}
