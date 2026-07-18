# Contributing

Thanks for your interest in PC Game Preservation Studio.

## Ground rules

- This project archives files users already legally own. Contributions that
  add DRM bypass, executable patching, platform-authentication emulation, or
  anything else that facilitates piracy will not be accepted, regardless of
  how they're framed.
- Match the existing architecture: the UI depends only on interfaces in
  `PcGamePreservationStudio.Core`; platform-, burning-, and IO-specific code
  stays in its own project.
- New interfaces/implementations should come with unit tests. Use mocks or
  abstractions for optical drives, the file system, and external processes
  so tests don't require physical hardware.

## Development setup

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. `dotnet restore`
3. `dotnet build`
4. `dotnet test`
5. `dotnet run --project src/PcGamePreservationStudio.App`

## Pull requests

- Keep PRs scoped to one phase or one fix at a time.
- Run `dotnet test` before opening a PR — CI runs the same command.
- Update `CHANGELOG.md` under `[Unreleased]`.

## Third-party dependencies

Any new NuGet dependency must have an MIT-compatible license, and its
license should be noted in the PR description.
