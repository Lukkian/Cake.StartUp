#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0
#tool nuget:?package=GitVersion.CommandLine&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=12.0.1
#tool nuget:?package=gitreleasemanager&version=0.8.0

// Squirrel: It's like ClickOnce but Works
#tool nuget:?package=squirrel.windows&version=1.9.1
#addin nuget:?package=Cake.Squirrel&version=0.14.0

// To debug in dotnet core [preliminar support], otherwise use: #addin nuget:?package=Cake.Powershell&version=0.4.7
//#addin nuget:?package=Lukkian.Cake.Powershell&version=0.4.9
#addin nuget:?package=Cake.Powershell&version=0.4.7

// To debug in VSCode uncomment below lines
// #r ".\tools\Addins\Lukkian.Cake.Powershell.0.4.9\lib\netcoreapp2.1\Cake.Core.Powershell.dll"

//////////////////////////////////////////////////////////////////////
// Fetch the most recent bootstrapper file from the resources repository using:
//////////////////////////////////////////////////////////////////////

// Invoke-WebRequest https://cakebuild.net/download/bootstrapper/windows -OutFile build.ps1

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

//.\build.ps1 --configuration="Debug"
//.\build.ps1 --configuration="Debug" --publish=true

var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");
var publish = Argument("publish", (string)null);

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solution = "Cake.StartUp";
var main_project_name = "WindowsFormsApp";
var test_target = "*tests";
var artifacts = "./artifacts";
var solution_path = $"./src/{solution}.sln";
var git_version = (GitVersion)null;

var gh_owner = "Lukkian";
var gh_repo = "Cake.StartUp";
var release_branch = "master";
var gh_token = EnvironmentVariable("gh_token") ?? Argument("gh_token", (string)null);
var grm_log = $"{artifacts}/GitReleaseManager.log";
var release_files = (string)null;

//////////////////////////////////////////////////////////////////////
// CUSTOM FUNCTIONS
//////////////////////////////////////////////////////////////////////

bool IsReleaseMode() => StringComparer.OrdinalIgnoreCase.Equals(configuration, "Release");
bool ShouldPatchAssemblyInfo() => AppVeyor.IsRunningOnAppVeyor;
bool ShouldPublishReleaseOnGitHub()
{
    var haveVersion = git_version != null;

    var forcePublish = string.IsNullOrWhiteSpace(publish) == false;
    
    var isInReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals(release_branch, git_version?.BranchName);

    if(haveVersion && (forcePublish || isInReleaseBranch))
    {
        if(AppVeyor.IsRunningOnAppVeyor && AppVeyor.Environment.Repository.Tag.IsTag == false)
        {
            return false;
        }

        return true;
    }

    return false;
}
bool HaveGitHubCredentials()
{
    var haveGHC = !string.IsNullOrWhiteSpace(gh_owner);
    haveGHC = haveGHC && !string.IsNullOrWhiteSpace(gh_repo);
    haveGHC = haveGHC && !string.IsNullOrWhiteSpace(gh_token);
    return haveGHC;
}
bool HasErrors() => Context.Data.Get<BuildData>().HasError;

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup<BuildData>(ctx =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	Information($"Solution: {solution_path}");
	Information($"Main project: ./src/{main_project_name}/{main_project_name}.csproj");
    Information($"Mode: {configuration}");

    return new BuildData();
});

//////////////////////////////////////////////////////////////////////
// TEARDOWN
//////////////////////////////////////////////////////////////////////
Teardown<BuildData>((ctx, data) =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");

    Warning("To create a release everything below should be True except HasErros");
    Warning("ShouldPublishReleaseOnGitHub: {0}", ShouldPublishReleaseOnGitHub());
    Warning("HaveGitHubCredentials: {0}",        HaveGitHubCredentials());
    Warning("IsReleaseMode: {0}",                IsReleaseMode());
    Warning("HasErrors: {0}",                    HasErrors());

    if(git_version != null)
    {
        Warning("****************************************");
        Warning($"NugetVersion: {git_version.NuGetVersionV2}");
        Warning($"FullSemVer: {git_version.FullSemVer}");
        Warning($"InformationalVersion: {git_version.InformationalVersion}");
        Warning("****************************************");
    }
    else
    {
        Error("****************************************");
        Error($"GitVersion is null");
        Error("****************************************");
    }

    if (data.HasError)
    {
        Error("There were one or more errors while executing the build tasks");
        foreach(var error in data.Errors)
        {
            Error($"  >{error}");
        }
    }
    else
    {
        Information("There were no errors during the execution of the build tasks");
    }
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    var solutionObj = ParseSolution(solution_path);
    var projects = solutionObj.Projects;
    var projectPaths = projects.Select(p => p.Path.GetDirectory());
    //var testAssemblies = projects.Where(p => p.Name.Contains("Tests")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
    //var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));

    // Clean solution directories
    foreach(var path in projectPaths)
    {
        Information($"Cleaning {path}/(bin|obj)");
        CleanDirectories($"{path}/**/bin");
        CleanDirectories($"{path}/**/obj");
    }

    // Clean artifacts directories
    Information($"Cleaning: {artifacts}/releases");
    CleanDirectory($"{artifacts}/releases");
    Information($"Cleaning: {artifacts}/nuget");
    CleanDirectory($"{artifacts}/nuget");
});

Task("RunGitVersion")
    .Does(() =>
{
    git_version = GitVersion(new GitVersionSettings {
        RepositoryPath = ".",
        LogFilePath = $"{artifacts}/GitVersion.log",
        OutputType = GitVersionOutput.Json,
        UpdateAssemblyInfo = ShouldPatchAssemblyInfo()
    });
    
    Information($"Full GitVersion: {Newtonsoft.Json.JsonConvert.SerializeObject(git_version)}");
});

Task("CheckAndUpdateAppVeyorBuild")
    .IsDependentOn("RunGitVersion")
    .Does(() =>
{
    if (AppVeyor.IsRunningOnAppVeyor)
    {
        //StartPowershellFile("./appveyor.ps1", args => { args.Append("Version", $"{gitVersion.FullSemVer}"); });
        StartPowershellFile("./appveyor.ps1", args => { args.Append("Version", $"{git_version.SemVer}+{AppVeyor.Environment.Build.Number}"); });
        Information($"AppVeyor Info");
        Information($"    Folder: {AppVeyor.Environment.Build.Folder}");
        Information($"    Number: {AppVeyor.Environment.Build.Number}");
    }
    else
    {
        Information("Not running on AppVeyor");
    }
});

Task("RestoreNuGetPackages")
    .Does(() =>
{
    // Restore all NuGet packages.
    Information("Restore all NuGet packages...");
    NuGetRestore(solution_path);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("RestoreNuGetPackages")
    .IsDependentOn("CheckAndUpdateAppVeyorBuild")
    .Does(() =>
{
	Information("Building solution...");
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solution_path, settings =>
        settings.SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
    }
    else
    {
      // Use XBuild
      XBuild(solution_path, settings =>
        settings.SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
    }
});

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information($"Pattern: {test_target}");
    NUnit3($"./src/**/bin/{configuration}/{test_target}.dll", new NUnit3Settings { NoResults = true });
});

Task("NuGetPackage")
    .IsDependentOn("UnitTests")
    .IsDependentOn("RunGitVersion")
    .WithCriteria(() => IsReleaseMode())
    .Does(() =>
{
    var nuGetPackSettings   = new NuGetPackSettings {
        Id                      = main_project_name.Replace(".", string.Empty),
        Version                 = git_version.NuGetVersionV2,
        Verbosity               = NuGetVerbosity.Detailed,
        Title                   = "Windows Forms App",
        Authors                 = new[] {"Lukkian"},
        Description             = "My app description.",
        IconUrl                 = new Uri("file:///" + MakeAbsolute(File($"./src/{main_project_name}/cake.ico")).FullPath),
        Files                   = new [] {
                new NuSpecContent {Source = "DeltaCompressionDotNet.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "DeltaCompressionDotNet.MsDelta.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "DeltaCompressionDotNet.PatchApi.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Mono.Cecil.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Mono.Cecil.Mdb.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Mono.Cecil.Pdb.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Mono.Cecil.Rocks.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "NuGet.Squirrel.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "SharpCompress.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Splat.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = "Squirrel.dll", Target = @"lib\net45"},
                new NuSpecContent {Source = $"{main_project_name}.exe", Target = @"lib\net45"},
                new NuSpecContent {Source = $"{main_project_name}.exe.config", Target = @"lib\net45"},
            },
        ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos", $"{git_version.NuGetVersionV2} release notes"},
        BasePath                = $"./src/{main_project_name}/bin/{configuration}",
        OutputDirectory         = $"{artifacts}/nuget"
    };

    NuGetPack($"./src/{main_project_name}/{main_project_name}.nuspec", nuGetPackSettings);
});

Task("SquirrelPackage")
    .IsDependentOn("NuGetPackage")
    .IsDependentOn("RunGitVersion")
    .WithCriteria(() => IsReleaseMode())
	.Does(() => 
{
    var squirrelSettings = new SquirrelSettings {
        LoadingGif = $"./src/{main_project_name}/loading.gif",
        ReleaseDirectory = $"{artifacts}/releases",
        FrameworkVersion = "net472",
    };
    Squirrel(File($"{artifacts}/nuget/{main_project_name}.{git_version.NuGetVersionV2}.nupkg"), squirrelSettings);
    Information($"Squirrel package for version {git_version.NuGetVersionV2} created on folder: {squirrelSettings.ReleaseDirectory}");

    var files = GetFiles($"{artifacts}/releases/*");
    foreach(var file in files)
    {
        Information("   File: {0}", file);
    }
    release_files = files.Select(f => f.GetFilename()).Aggregate((a, b) => $"{a},{b}").ToString();
});

Task("CreateGitHubRelease")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("SquirrelPackage")
    .WithCriteria(() => ShouldPublishReleaseOnGitHub())
    .WithCriteria(() => HaveGitHubCredentials())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    GitReleaseManagerCreate(gh_token, gh_owner, gh_repo, new GitReleaseManagerCreateSettings {
        //Milestone         = gitVersion.SemVer,
        Name              = $"v{git_version.SemVer}",
        InputFilePath     = "RELEASENOTES.md",
        Prerelease        = false,
        TargetCommitish   = "master",
        WorkingDirectory  = $"{artifacts}/releases",
        ToolTimeout       = TimeSpan.FromSeconds(120),
        LogFilePath       = grm_log
    });
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

Task("AttachGitHubReleaseArtifacts")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("SquirrelPackage")
    .IsDependentOn("CreateGitHubRelease")
    .WithCriteria(() => ShouldPublishReleaseOnGitHub())
    .WithCriteria(() => HaveGitHubCredentials())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    GitReleaseManagerAddAssets(gh_token, gh_owner, gh_repo, $"v{git_version.SemVer}",
        release_files,
        new GitReleaseManagerAddAssetsSettings {
            WorkingDirectory  = $"{artifacts}/releases",
            ToolTimeout       = TimeSpan.FromSeconds(120),
            LogFilePath       = grm_log
        });
    Information("Files attached to the release: {0}", release_files);
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("AttachGitHubReleaseArtifacts");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

public class BuildData
{
    public bool HasError { get; set; }
    public IList<string> Errors { get => _errors; }
    private IList<string> _errors;

    public BuildData()
    {
        _errors = new List<string>();
    }

    public void AddError(string error)
    {
        _errors.Add(error);
    }
}