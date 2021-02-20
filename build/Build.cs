using System;
using System.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });


    Target BuildDockerImage => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DockerBuild(o => o
                .SetTag($"eileen:{GitVersion.Sha}", "eileen:latest")
                .SetPath(Solution.Path.Parent));
        });

    Target RemoveExistingImage => _ => _
        .After(Compile)
        .ProceedAfterFailure()
        .Executes(() => 
        {
            Docker("rmi eileen:latest", logOutput: false);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            // DotNetBuild(s => s
            //     .SetProjectFile(Solution)
            //     .SetAssemblyVersion(GitVersion.AssemblySemVer)
            //     .SetFileVersion(GitVersion.AssemblySemFileVer)
            //     .SetInformationalVersion(GitVersion.InformationalVersion)
            //     .SetOutputDirectory(OutputDirectory)
            //     .EnableNoRestore());
        });

}
