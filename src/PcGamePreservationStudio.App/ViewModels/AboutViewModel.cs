using System.Reflection;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string ApplicationName => "PC Game Preservation Studio";

    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0-dev";

    public string LegalNotice =>
        "PC Game Preservation Studio creates personal archival copies of files already available to the " +
        "user. It does not remove DRM, emulate platform services, bypass ownership checks, or guarantee " +
        "that a game will operate without its original launcher, account, activation service, or online " +
        "features. Users are responsible for complying with applicable laws, licenses, and platform agreements.";

    public string License => "Licensed under the MIT License.";
}
