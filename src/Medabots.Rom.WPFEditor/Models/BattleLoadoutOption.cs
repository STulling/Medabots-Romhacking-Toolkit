using System.Windows.Media.Imaging;

namespace Medabots.Rom.WPFEditor.Models;

public sealed record BattleLoadoutOption(int Id, int PartId, string Title, string Subtitle, BitmapSource Thumbnail);
