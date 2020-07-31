using System;
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

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("The Discord Bot Token to use when building the Docker file")]
    readonly string DiscordApiToken;

    [Parameter("Overrides the command prefix used to fire off commands for the bot")]
    readonly string CommandPrefix = "!";

    [Parameter("The text to look for in a sentence that will trigger the Markov chain builder for the bot")]
    readonly string MarkovTrigger = "erector";


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

    Target GenerateManifestFile => _ => _
        .DependsOn(Clean)
        .Requires(() => !string.IsNullOrWhiteSpace(DiscordApiToken))
        .Executes(() => 
        {
            // Generate a Dockerfile
            var rootDirectory = Solution.Path.ToString();
            
            var dockerTemplate = File.ReadAllText(Path.Combine("build", "Dockerfile.Template"));
            var credentials = File.ReadAllText(Path.Combine("build", "resources", "credentials.json"));
            var tokenSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"DiscordApiToken", DiscordApiToken},
                {"Credentials", credentials},
                {"CommandPrefix", CommandPrefix},
                {"MarkovTrigger", MarkovTrigger}
            };
            var dockerFile = Regex.Replace(dockerTemplate, @"\$\((.*?)\)", (match) =>
            {
                var pMatch = match.Groups[1];
                return tokenSource.ContainsKey(pMatch.Value) ?
                    tokenSource[pMatch.Value].Replace("\"", "\\\"") :
                    match.Value;
            });
            File.WriteAllText("Dockerfile", dockerFile);
        });

    Target BuildDockerImage => _ => _
        .DependsOn(GenerateManifestFile)
        .DependsOn(Compile)
        .Executes(() =>
        {
            DockerBuild(o => o
                .SetTag($"eileen:{GitVersion.Sha}")
                .SetPath(Solution.Path.Parent));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Requires(() => !string.IsNullOrWhiteSpace(DiscordApiToken))
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetOutputDirectory(OutputDirectory)
                .EnableNoRestore());
        });

}
