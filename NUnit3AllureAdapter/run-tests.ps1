#cd ..

$ProjectDir = "."
$testDir = "$ProjectDir"


# Set NUnit3 test runner path
$nunit3 = "$ProjectDir\packages\NUnit.ConsoleRunner.3.6.1\tools\nunit3-console.exe"

try
{
    #Unit tests
    $fullUnitTestsPath = (Get-ChildItem $testDir -Recurse -Include *Tests*.dll) | where { $_.FullName -match "\\bin($|\\)" } 
    $fullUnitTestsPath -split(" ")
    & $nunit3 $fullUnitTestsPath --framework=net-4.5 --result='TestsResult2.xml;format=nunit2' --verbose --workers=2
    Write-Host "Press any key to continue ..."
    $x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    
    if( $LastExitCode -ne 0 ){EXIT $LastExitCode}
}
catch
{
	Write-Error $error[0]
    exit($LastExitCode)
}
finally
{
cd ..
}