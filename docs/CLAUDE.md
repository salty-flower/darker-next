# .NET Template

This is repo is freshly brewed from my dotnet template!
Evolve this document as you move forward.

## Build System

It adopts centralized package management and injects many good default packages and project settings via `Directory.{Build,Packages}.{targets,props}`.
Zero initial config is needed for new `.csproj` - `<Project />` is enough!

## Formatter

`csharpier` for both C# and XML.
