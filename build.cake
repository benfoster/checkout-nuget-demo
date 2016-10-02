//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine"
#addin "MagicChunks"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");


///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var isLocalBuild        = !AppVeyor.IsRunningOnAppVeyor;
var packPath            = Directory("./src/NugetDemo");
var sourcePath          = Directory("./src");
var testsPath           = Directory("test");
var buildArtifacts      = Directory("./artifacts");

var isContinuousIntegrationBuild = !BuildSystem.IsLocalBuild;

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

var nugetVersion = isContinuousIntegrationBuild ? gitVersionInfo.NuGetVersion : "0.0.0";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    Information("Building DotNetCoreBuild v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Default")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Test")
    .IsDependentOn("__UpdateProjectJsonVersion")
    .IsDependentOn("__Pack");

Task("__Clean")
    .Does(() =>
{
    CleanDirectories(new DirectoryPath[] { buildArtifacts });
});

Task("__Restore")
    .Does(() =>
{
    var settings = new DotNetCoreRestoreSettings
    {
        Sources = new [] { "https://api.nuget.org/v3/index.json" }
    };

    DotNetCoreRestore(sourcePath, settings);
    DotNetCoreRestore(testsPath, settings);
});

Task("__UpdateAssemblyVersionInformation")
    .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
     GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        //UpdateAssemblyInfoFilePath = globalAssemblyFile
    });

    Information("AssemblyVersion -> {0}", gitVersionInfo.AssemblySemVer);
    Information("AssemblyFileVersion -> {0}", $"{gitVersionInfo.MajorMinorPatch}.0");
    Information("AssemblyInformationalVersion -> {0}", gitVersionInfo.InformationalVersion);
});

Task("__Build")
    .Does(() =>
{
	var projects = GetFiles("./**/project.json");

	foreach(var project in projects)
	{
        var settings = new DotNetCoreBuildSettings 
        {
            Configuration = configuration
        };

	    DotNetCoreBuild(project.GetDirectory().FullPath, settings); 
    }
});

Task("__Test")
    .Does(() =>
{
    var projects = GetFiles("./test/**/project.json");

    foreach(var project in projects)
	{
        var settings = new DotNetCoreTestSettings
        {
            Configuration = configuration
        };

        if (!IsRunningOnWindows())
        {
            Information("Not running on Windows - skipping tests for full .NET Framework");
            settings.Framework = "netcoreapp1.0";
        }

        DotNetCoreTest(project.GetDirectory().FullPath, settings);
    }
});

Task("__UpdateProjectJsonVersion")
    .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
    var projectToPackagePackageJson = $"{packPath}/project.json";
    Information("Updating {0} version -> {1}", projectToPackagePackageJson, nugetVersion);

    TransformConfig(projectToPackagePackageJson, projectToPackagePackageJson, new TransformationCollection {
        { "version", nugetVersion }
    });
});

Task("__Pack")
    .Does(() =>
{
    var settings = new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = buildArtifacts,
    };

    // add build suffix for CI builds
    if(!isLocalBuild && !AppVeyor.Environment.Repository.Tag.IsTag)
    {
        settings.VersionSuffix = "build" + AppVeyor.Environment.Build.Number.ToString().PadLeft(5,'0');
    }

    DotNetCorePack(packPath, settings);
});


Task("ReleaseNotes")
    .IsDependentOn("__Pack")
    .Does(() => 
{
    FilePath changeLogPath = File("./artifacts/changelog.md");
    IEnumerable<string> lines;
    var exitCode = StartProcess("git", new  ProcessSettings { Arguments = "log --no-merges --oneline --decorate --pretty=format:\"* %s\"", RedirectStandardOutput = true }, out lines);
    if (exitCode == 0)
    {
        using(var stream = Context.FileSystem.GetFile(changeLogPath).OpenWrite())
        {
            using(var writer = new System.IO.StreamWriter(stream, Encoding.UTF8))
            {
                foreach(var line in lines)
                {
                    writer.WriteLine(line);
                }
            }
        }
    }
});

Task("GitHubRelease")
    .IsDependentOn("ReleaseNotes")
    .Does(() => 
{
    var settings = new GitReleaseManagerCreateSettings  
    {
        InputFilePath = "./artifacts/changelog.md",
        Prerelease = false,
        Name =  "1.0.5",
        TargetCommitish = "master" // The commit to tag. Can be a branch or SHA. Defaults to repository's default branch.
    };

    GitReleaseManagerCreate(EnvironmentVariable("CAKE_GITHUB_USERNAME"), EnvironmentVariable("CAKE_GITHUB_TOKEN"), "ben-foster-cko", "checkout-nuget-demo", settings);
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("__Default");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);