param (
    [Parameter(Mandatory = $true)][string]$version
)

# validate arguments 
$r = [System.Text.RegularExpressions.Regex]::Match($Version, "^[0-9]+(\.[0-9]+){1,3}$");

if ($r.Success) {
    Update-AppveyorBuild -Version "$version"
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

