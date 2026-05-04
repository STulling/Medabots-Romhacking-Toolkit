using Medabots.Rom.Metadata;

namespace Medabots.Rom.Projects;

public sealed class ResolvedRomLayout
{
    public ResolvedRomLayout(MedabotsRomTextProfile? textAndEventProfile)
    {
        TextAndEventProfile = textAndEventProfile;
    }

    public MedabotsRomTextProfile? TextAndEventProfile { get; private set; }

    public MedabotsRomTextProfile RequireTextAndEventProfile()
    {
        return TextAndEventProfile ?? throw new InvalidOperationException("The project does not define a known text/event profile.");
    }

    public void ReplaceTextAndEventProfile(MedabotsRomTextProfile profile)
    {
        TextAndEventProfile = profile ?? throw new ArgumentNullException(nameof(profile));
    }
}
