#r "paket:
framework: auto-detect
source https://www.nuget.org/api/v2
source https://api.nuget.org/v3/index.json

nuget FSharp.Core 4.7.0.0
nuget Fake.DotNet.Cli
nuget Fake.Tools.Git
nuget Fake.Api.GitHub
nuget Fake.Core.Environment
nuget Fake.Core.Process
nuget Fake.Core.ReleaseNotes
nuget Fake.Core.SemVer
nuget Fake.Core.String
nuget Fake.Core.Target //"
#load "./.fake/build.fsx/intellisense.fsx"
#if !FAKE
  #r "Facades/netstandard"
#endif

open System
open System.IO

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git


// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "FsStorm" 
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "FsShelter"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" "https://raw.github.com/"+gitOwner+"/"+gitName

// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "Clean" (fun _ ->
    !! "./**/bin"
    ++ "./**/obj"
    ++ "./docs/output"
    |> Seq.iter Shell.cleanDir
)

Target.create "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<PackageProjectUrl>https://github.com/FsStorm/FsShelter</PackageProjectUrl>"
      "<PackageLicenseUrl>https://raw.githubusercontent.com/FsStorm/FsShelter/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl>https://raw.githubusercontent.com/FsStorm/FsShelter/master/docs/files/img/logo.png</PackageIconUrl>"
      "<RepositoryUrl>https://github.com/FsStorm/FsShelter.git</RepositoryUrl>"
      "<PackageTags>storm;cep;event-driven;fsharp;distributed</PackageTags>"
      "<PackageDescription>F# DSL and runtime for Apache Storm topologies</PackageDescription>"
      "<Authors>Eugene Tolmachev</Authors>"
      sprintf "<PackageReleaseNotes>%s</PackageReleaseNotes>" (List.head release.Notes |> System.Web.HttpUtility.HtmlEncode)
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> File.write false "Directory.Build.props"
)

Target.create "Restore" (fun _ ->
    DotNet.restore id "src"
)

Target.create "Build" (fun _ ->
    DotNet.build (fun opt -> { opt with Configuration = DotNet.BuildConfiguration.Release }) "src"
)

Target.create "Tests" (fun _ ->
    let args = "--no-restore --filter \"TestCategory!=interactive\""
    DotNet.test (fun a -> a.WithCommon (fun c -> { c with CustomParams = Some args})) "src/FsShelter.Tests"
)

Target.create "Package" (fun _ ->
    let args = sprintf "/p:Version=%s --no-restore" (string release.SemVer)
    DotNet.pack (fun a -> a.WithCommon (fun c -> { c with CustomParams = Some args })) "src/FsShelter"
)

Target.create "PublishNuget" (fun _ ->
    let exec dir = DotNet.exec (fun a -> a.WithCommon (fun c -> { c with WorkingDirectory=dir }))
    let result = exec "src/FsShelter" "nuget" (sprintf "push bin/Release/FsShelter.%s.nupkg -s nuget.org -k %s" release.NugetVersion (Environment.environVar "nugetkey"))
    if (not result.OK) then failwithf "%A" result.Errors
)


// --------------------------------------------------------------------------------------
// Generate the documentation

let fsdocParameters = [
  sprintf "fsdocs-release-notes-link %s/FsShelter/blob/master/RELEASE_NOTES.md" gitHome
  sprintf "fsdocs-license-link %s/FsShelter/blob/master/LICENSE.md" gitHome
  "fsdocs-navbar-position fixed-left"
]

let fsdocProperties = [
  "Configuration=Release"
  "TargetFramework=netstandard2.0"
]

Target.create "GenerateDocs" (fun _ ->
    Shell.cleanDir ".fsdocs"
    DotNet.exec id "fsdocs" ("build --strict --eval --clean"
      + " --projects src/FsShelter/FsShelter.fsproj" 
      + " --properties " + String.Join(" ",fsdocProperties) 
      + " --parameters " + String.Join(" ", fsdocParameters)) |> ignore
)

Target.create "WatchDocs" (fun _ ->
    Shell.cleanDir ".fsdocs"
    DotNet.exec id "fsdocs" ("watch --eval"
      + " --projects src/FsShelter/FsShelter.fsproj" 
      + " --properties " + String.Join(" ",fsdocProperties) 
      + " --parameters " + String.Join(" ", fsdocParameters)) |> ignore
)

Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Repository.cloneSingleBranch "" ("git@github.com:FsStorm/FsShelter.git") "gh-pages" tempDocsDir

    Repository.fullclean tempDocsDir
    Shell.copyRecursive "output" tempDocsDir true |> Trace.tracefn "%A"
    Staging.stageAll tempDocsDir
    Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" (string release.SemVer))
    Branches.push tempDocsDir
)

// --------------------------------------------------------------------------------------
// code-gen tasks
Target.create "ProtoShell" (fun _ ->
    let generated = "ext" @@ "ProtoShell" @@ "generated" 
    let cli = 
        "packages" @@ "build" @@ "Google.Protobuf.Tools" @@ "tools" 
        @@ if Environment.isWindows then "windows_x64" @@ "protoc.exe"
           else if Environment.isLinux then "linux_x64" @@ "protoc"
           else "macosx_x64" @@ "protoc"
    Shell.cleanDir generated
    Shell.Exec(
            cli,
            "--csharp_out=" + generated 
            + " --proto_path=" + "packages" @@ "build" @@ "Google.Protobuf.Tools" @@ "tools"
            + " --proto_path=" + "paket-files" @@ "FsStorm" @@ "protoshell" @@ "src" @@ "main" @@  "proto"
            + " paket-files" @@ "FsStorm" @@ "protoshell" @@ "src" @@ "main" @@  "proto" @@ "multilang.proto",
            ".")
    |> ignore
)

Target.create "StormThriftNamespace" (fun _ ->
    "paket-files" @@ "et1975" @@ "storm" @@ "storm-core" @@ "src" @@ "storm.thrift"
    |> Shell.regexReplaceInFileWithEncoding "namespace java org.apache.storm.generated" "namespace csharp StormThrift" Text.Encoding.ASCII
)

Target.create "StormThrift" (fun _ ->
    let generated = "ext" @@ "StormThrift" @@ "StormThrift"
    Shell.cleanDir generated
    Shell.Exec(
            "packages" @@ "build" @@ "Thrift" @@ "tools" @@ "thrift-0.9.1.exe",
            "-out " + generated @@ ".."
            + " --gen csharp"
            + " paket-files" @@ "et1975" @@ "storm" @@ "storm-core" @@ "src" @@ "storm.thrift",
            ".")
    |> ignore
)

Target.create "GenerateSources" ignore

"ProtoShell"
  ==> "GenerateSources"
"StormThriftNamespace"
  ==> "StormThrift"
  ==> "GenerateSources"

// --------------------------------------------------------------------------------------
// graph gen tasks
// GraphViz has to be installed and "dot" be in the path
let exportGraph (app:string) (arg:string) fileName =
    Shell.Exec(
            "dotnet",
            ("samples" @@ app @@ "bin" @@ "Release" @@ "netcoreapp2.1" @@ (sprintf "%s.dll" app)) +
            (sprintf " %s | dot -Tsvg -o samples/%s/obj/%s.svg" arg app fileName),
            ".")
    |> ignore

Target.create "WordCountSvg" (fun _ ->
    exportGraph "WordCount" "graph" "WordCount"
)

Target.create "WordCountColourSvg" (fun _ ->
    exportGraph "WordCount" "colour-graph" "WordCount_colourized"
)

Target.create "WordCountCustomColourSvg" (fun _ ->
    exportGraph "WordCount" "custom-colour-graph" "WordCount_custom_colourized"
)

Target.create "GuaranteedSvg" (fun _ ->
    exportGraph "Guaranteed" "graph" "Guaranteed"
)

Target.create "ExportGraphs" ignore
Target.create "All" ignore
Target.create "Release" ignore

"ExportGraphs"
    <== ["Build"; "WordCountSvg"; "WordCountColourSvg"; "WordCountCustomColourSvg"; "GuaranteedSvg"]

"All"
  <== ["Clean"; "Restore"; "Meta"; "Build"; "Tests"; "Package"; "GenerateDocs"]

"Build"
  ==> "Tests"
 
"Build"
  ==> "GenerateDocs"

"Meta"
  ==> "Build"
  ==> "Package"
  ==> "PublishNuget"
    
"Release"
  <== ["All"; "PublishNuget"; "ReleaseDocs"]

Target.runOrDefault "All"
