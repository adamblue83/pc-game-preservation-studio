namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>
/// GOG installed-game and offline-installer detection. Phase 5, not yet implemented.
/// Deliberately just a marker specialization of <see cref="IGamePlatformProvider"/> so an
/// official authenticated download integration could be layered on later without collecting
/// credentials today.
/// </summary>
public interface IGogLibraryProvider : IGamePlatformProvider
{
}
