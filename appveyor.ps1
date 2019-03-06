param (
    [Parameter(Mandatory = $true)][string]$version
)

Update-AppveyorBuild -Version "$version"