// include Fake lib
#r @"tools/FAKE/tools/FakeLib.dll"

open Fake 
open Fake.EnvironmentHelper
open System.IO



let project = "FactStore"
let project_description = "Fact/Event store library"
let project_summary = "JIT Fact Store is an event store"
let project_tags = ""
let target_framework = "net40"
let nuget_projects = ["FactStore"; "FactStore.Implementations"]

let product_version = "0.1"
let copyright = "Copyright Jander IT 2014"
let authors = ["Jander IT"]

let buildnumber = match buildServer with 
                  | Jenkins -> jenkinsBuildNumber.PadLeft(5, '0')
                  | _ -> "00000"

let buildDir = "./build/"
let packagingDir = "./temp/"
let deployDir = "./deploy/"
                                   
let version = sprintf "%s.%s-dev" product_version buildnumber

let nunitPath = "tools"@@"NUnit.Runners"@@"tools"

RestorePackages()