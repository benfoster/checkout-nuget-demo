#tool "nuget:?package=GitReleaseNotes"

var target          = Argument("target", "Default");
var configuration   = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var isLocalBuild        = !AppVeyor.IsRunningOnAppVeyor;
var packPath            = Directory("./src/NugetDemo");
var sourcePath          = Directory("./src");
var testsPath           = Directory("test");
var buildArtifacts      = Directory("./artifacts");

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
	var projects = GetFiles("./**/project.json");

	foreach(var project in projects)
	{
        var settings = new DotNetCoreBuildSettings 
        {
            Configuration = configuration
            // Runtime = IsRunningOnWindows() ? null : "unix-x64"
        };

	    DotNetCoreBuild(project.GetDirectory().FullPath, settings); 
    }
});

Task("RunTests")
    .IsDependentOn("Restore")
    .IsDependentOn("Clean")
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

Task("Pack")
    .IsDependentOn("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    var settings = new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = buildArtifacts,
    };
    
    Information("AppVeyor Build Version: " + AppVeyor.Environment.Build.Version);
    Information("AppVeyor Build Number: " + AppVeyor.Environment.Build.Number.ToString());

    // add build suffix for CI builds
    if(!isLocalBuild && !AppVeyor.Environment.Repository.Tag.IsTag)
    {
        settings.VersionSuffix = "build" + AppVeyor.Environment.Build.Number.ToString().PadLeft(5,'0');
    }

    DotNetCorePack(packPath, settings);
});

Task("Clean")
    .Does(() =>
{
    CleanDirectories(new DirectoryPath[] { buildArtifacts });
});

Task("Restore")
    .Does(() =>
{
    var settings = new DotNetCoreRestoreSettings
    {
        Sources = new [] { "https://api.nuget.org/v3/index.json" }
    };

    DotNetCoreRestore(sourcePath, settings);
    DotNetCoreRestore(testsPath, settings);
});

Task("ReleaseNotes")
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

Task("Default")
  .IsDependentOn("Build")
  .IsDependentOn("RunTests")
  .IsDependentOn("Pack")
  .IsDependentOn("ReleaseNotes");

RunTarget(target);