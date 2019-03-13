# SetAssemblyInfoVersion.ps1
#
# Set the version in all the AssemblyInfo.cs or AssemblyInfo.vb files in any subdirectory.
#
# usage:  
#  from cmd.exe: 
#     powershell.exe .\SetAssemblyInfoVersion.ps1  2.8.3.0
# 
#  from powershell.exe prompt: 
#     .\SetAssemblyInfoVersion.ps1  2.8.3.0
#
#

param (
    [Parameter(Mandatory = $true)][string]$version
)

function Usage {
    Write-Host "Usage: ";
    Write-Host "  from cmd.exe: ";
    Write-Host "     powershell.exe SetAssemblyInfoVersion.ps1  2.8.3.0";
    Write-Host " ";
    Write-Host "  from powershell.exe prompt: ";
    Write-Host "     .\SetAssemblyInfoVersion.ps1  2.8.3.0";
    Write-Host " ";
}

function Update-SourceVersion {
    Param ([string]$Version)
    $NewVersion = 'AssemblyVersion("' + $Version + '")';
    $NewFileVersion = 'AssemblyFileVersion("' + $Version + '")';
    $NewInformationalVersion = 'AssemblyInformationalVersion("' + $Version + '")';

    foreach ($o in $input) {
        Write-Host $o.FullName
        $TmpFile = $o.FullName + ".tmp"

        get-content $o.FullName -encoding utf8 | 
            ForEach-Object {$_ -replace 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $NewVersion } |
            ForEach-Object {$_ -replace 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $NewFileVersion }  |
            ForEach-Object {$_ -replace 'AssemblyInformationalVersion\(([^\)]+)\)', $NewInformationalVersion }  > $TmpFile
     
        # Preserve UTF-8 encoding of AssemblyInfo tmp file
        $utf8Content = Get-Content $TmpFile -encoding utf8
        [System.IO.File]::WriteAllLines($TmpFile, $utf8Content)

        move-item $TmpFile $o.FullName -force
    }
}

function Update-AllAssemblyInfoFiles ( $version ) {
    foreach ($file in "AssemblyInfo.cs", "AssemblyInfo.vb" ) {
        get-childitem -recurse | Where-Object {$_.Name -eq $file} | Update-SourceVersion $version ;
    }
}

# validate arguments 
$r = [System.Text.RegularExpressions.Regex]::Match($Version, "^[0-9]+(\.[0-9]+){1,3}$");

if ($r.Success) {
    Update-AllAssemblyInfoFiles $version;
}
else {
    Write-Host " ";
    Write-Host "Bad Input for verion number!"
    Write-Host $version;
    Write-Host " ";
    Usage ;
    Write-Error "Bad Input for verion number: " + $version
    exit 1
}