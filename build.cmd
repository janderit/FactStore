@echo off
SET dopause=0
IF "%1"=="" (
  SET dopause=1
  SET target=Default
) ELSE (
  SET target=%1
)
cls

@IF NOT EXIST "tools\FAKE\tools\Fake.exe" (
	@echo "Installing FAKE from NUGET... (this may take a short while)"
	@"tools\nuget.exe" "install" "FAKE" "-OutputDirectory" "tools" "-ExcludeVersion"
)

@IF NOT EXIST "tools\NUnit.Runners\tools\nunit-console.exe" (
	@echo "Installing NUNIT from NUGET... (this may take a short while)"
	@"tools\nuget.exe" "install" "NUNIT.Runners" "-OutputDirectory" "tools" "-ExcludeVersion"
)

@echo Invoking build.fsx...
@"tools\FAKE\tools\Fake.exe" build_script.fsx %target%
IF "%dopause%"=="1" pause

