// include Fake lib
#r @"tools/FAKE/tools/FakeLib.dll"
#r "System.Xml.Linq.dll"

open Fake 
open Fake.EnvironmentHelper
open System.IO
open System.Xml.Linq

// Configuration

#load "build_config.fsx"
open Build_config

// ----------------------------------------------------------------------------------------------------
// --------------- no configurable parts beyond this line ---------------------------------------------
// ----------------------------------------------------------------------------------------------------

trace <| "Building version "+version

let nugetdeployTarget = environVarOrNone "NUGET_DEPLOY"

// helpers
let empty () = ()

//let readDependencies project = NugetTool.Dependencies.Find project |> Seq.map (fun dep -> dep.Name, dep.Version)
let readDependencies project =
    let package_file = project@@"packages.config"
    if (not (System.IO.File.Exists(package_file)))
        then
            List.empty
        else
            let package_to_dependency (package:XElement) = package.Attribute(XName.Get "id").Value, ("["+package.Attribute(XName.Get "version").Value+"]")
            let xdoc = XDocument.Load(package_file)
            let packages = xdoc.Element(XName.Get "packages")
            packages.Elements (XName.Get "package") |> Seq.map package_to_dependency |> Seq.toList

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir]
)

Target "BuildDebug" (fun _ ->
    !! "**/*.csproj"
    |> MSBuildDebug buildDir "Build"
    |> Log "AppBuild-Output: "
)

Target "Test" (fun _ ->

    let configuration = fun p -> { p with
                                    DisableShadowCopy = true;
                                    OutputFile = buildDir @@ "TestResults.xml"
                                    ToolPath = nunitPath
                                 }

    trace "-----------------------------------------------------------------"
    trace "----------------------- RUNNING TESTS ---------------------------"
    trace "-----------------------------------------------------------------"
    !! (buildDir @@ "*.dll") 
      |> NUnit configuration
)

Target "PackageNuGet" (fun _ ->
    // Copy all the package files into a package folder
    CreateDir packagingDir
    CreateDir deployDir

    let net45 = packagingDir @@ "lib" @@ "net45"
    CreateDir net45
    CopyFiles net45 [buildDir @@ project+".dll"]

    let deps = readDependencies project

    NuGet (fun p -> 
        {p with
            Authors = authors
            Project = project
            Description = project_description
            OutputPath = deployDir
            Summary = project_summary
            WorkingDir = packagingDir
            Copyright = copyright
            Version = version
            Publish = false
            Tags = project_tags
            Dependencies = deps |> Seq.toList
             }) 
            "template.nuspec"
)

Target "DeployNuget" (fun _ ->
    match nugetdeployTarget with
    | None -> failwith "NUGET_DEPLOY environment variable must be set for target 'DeployNuget'"
    | Some(target) -> CopyDir target deployDir (endsWith ".nupkg")
)

// Default target
Target "Default" empty
Target "Deploy" empty

// Dependencies
"Clean" ==> "BuildDebug" ==> "Test" ==> "PackageNuGet" ==> "Default"
"PackageNuGet" ==> "DeployNuget" ==> "Deploy"

// start build
RunTargetOrDefault "Default"
