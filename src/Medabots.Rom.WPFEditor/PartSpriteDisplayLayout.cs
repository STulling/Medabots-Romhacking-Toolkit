using Medabots.Rom.Images;
using Medabots.Rom.Parts;

namespace Medabots.Rom.WPFEditor;

internal static class PartSpriteDisplayLayout
{
    public static string[] GetBattleCompositeComponentNames() =>
    [
        "Head / Base",
        "Right Arm A",
        "Right Arm B",
        "Left Arm A",
        "Left Arm B",
        "Legs"
    ];

    public static IReadOnlyList<(int ComponentIndex, string Title)> GetPreviewComponentEntriesForPartKind(PartKind kind) => kind switch
    {
        PartKind.Head => [(0, "Battle Display")],
        PartKind.RightArm => [(1, "Battle Display A"), (2, "Battle Display B")],
        PartKind.LeftArm => [(3, "Battle Display A"), (4, "Battle Display B")],
        PartKind.Legs => [(5, "Battle Display")],
        _ => throw new InvalidOperationException($"Unsupported part kind '{kind}'.")
    };

    public static int GetLargeDisplayVariantSelectorForComponent(PartKind kind, int componentIndex) => kind switch
    {
        PartKind.RightArm => componentIndex == 2 ? 1 : 0,
        PartKind.LeftArm => componentIndex == 4 ? 1 : 0,
        _ => 0
    };

    public static string GetLargeDisplayVariantLabel(PartKind kind, int componentIndex) => kind switch
    {
        PartKind.RightArm => componentIndex == 2 ? "B" : "A",
        PartKind.LeftArm => componentIndex == 4 ? "B" : "A",
        _ => "Default"
    };

    public static string FormatPartKind(PartKind kind) => kind switch
    {
        PartKind.Head => "Head",
        PartKind.RightArm => "Right Arm",
        PartKind.LeftArm => "Left Arm",
        PartKind.Legs => "Legs",
        _ => kind.ToString()
    };

    public static bool AreEquivalentLargeDisplayAssets(LargePartDisplayAsset left, LargePartDisplayAsset right)
    {
        if (left.RootDescriptorId != right.RootDescriptorId || left.Pieces.Count != right.Pieces.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Pieces.Count; index++)
        {
            var leftPiece = left.Pieces[index];
            var rightPiece = right.Pieces[index];
            if (leftPiece.DescriptorId != rightPiece.DescriptorId ||
                leftPiece.ImageOffset != rightPiece.ImageOffset ||
                leftPiece.PaletteOffset != rightPiece.PaletteOffset ||
                leftPiece.X != rightPiece.X ||
                leftPiece.Y != rightPiece.Y ||
                leftPiece.Image.Width != rightPiece.Image.Width ||
                leftPiece.Image.Height != rightPiece.Image.Height)
            {
                return false;
            }
        }

        return true;
    }
}
