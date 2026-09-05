param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug','Release')]
    [System.String]$Target,
    
    [Parameter(Mandatory)]
    [System.String]$TargetPath,
    
    [Parameter(Mandatory)]
    [System.String]$TargetAssembly,

    [Parameter(Mandatory)]
    [System.String]$ValheimPath,

    [Parameter(Mandatory)]
    [System.String]$ProjectPath,

    [Parameter(Mandatory)]
    [System.String]$AssetBundlePath,
    
    [System.String]$DeployPath
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $AssetBundlePath -PathType Leaf)) {
    Write-Error -ErrorAction Stop -Message "Mod AssetBundle is missing: $AssetBundlePath"
}

function Copy-ModAssetBundle([string]$Destination) {
    Copy-Item -LiteralPath $AssetBundlePath -Destination $Destination -Force
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $sourceStream = [System.IO.File]::OpenRead($AssetBundlePath)
        try {
            $sourceHash = [System.BitConverter]::ToString($sha256.ComputeHash($sourceStream)).Replace("-", "")
        } finally {
            $sourceStream.Dispose()
        }

        $sha256.Initialize()
        $destinationStream = [System.IO.File]::OpenRead($Destination)
        try {
            $destinationHash = [System.BitConverter]::ToString($sha256.ComputeHash($destinationStream)).Replace("-", "")
        } finally {
            $destinationStream.Dispose()
        }
    } finally {
        $sha256.Dispose()
    }

    if ($sourceHash -ne $destinationHash) {
        Write-Error -ErrorAction Stop -Message "valheimmodassets verification failed after copying to $Destination"
    }

    Write-Host "Copied and verified valheimmodassets ($sourceHash) to $Destination"
}

function Remove-LegacyAssetBundle([string]$Directory) {
    $legacyPath = Join-Path $Directory "customfont"
    if (Test-Path -LiteralPath $legacyPath -PathType Leaf) {
        Remove-Item -LiteralPath $legacyPath -Force
        Write-Host "Removed obsolete AssetBundle: $legacyPath"
    }
}

# Make sure Get-Location is the script path
Push-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Path)

# Test some preliminaries
("$TargetPath",
 "$ValheimPath",
 "$(Get-Location)\..\libraries"
) | % {
    if (!(Test-Path "$_")) {Write-Error -ErrorAction Stop -Message "$_ folder is missing"}
}

# Plugin name without ".dll"
$name = "$TargetAssembly" -Replace('.dll')

# Create the mdb file
$pdb = "$TargetPath\$name.pdb"
if (Test-Path -Path "$pdb") {
    Write-Host "Create mdb file for plugin $name"
    Invoke-Expression "& `"$(Get-Location)\..\libraries\Debug\pdb2mdb.exe`" `"$TargetPath\$TargetAssembly`""
}

# Main Script
Write-Host "Publishing for $Target from $TargetPath"

if ($Target.Equals("Debug")) {
    if ($DeployPath.Equals("")){
      $DeployPath = "$ValheimPath\BepInEx\plugins"
    }
    
    $plug = New-Item -Type Directory -Path "$DeployPath\$name" -Force
    Write-Host "Copy $TargetAssembly to $plug"
    Copy-Item -Path "$TargetPath\$name.dll" -Destination "$plug" -Force
    Copy-Item -Path "$TargetPath\$name.pdb" -Destination "$plug" -Force
    Copy-Item -Path "$TargetPath\$name.dll.mdb" -Destination "$plug" -Force
    Copy-ModAssetBundle "$plug\valheimmodassets"
    Remove-LegacyAssetBundle $plug
}

if($Target.Equals("Release")) {
    Write-Host "Packaging for ThunderStore..."
    $Package="Package"
    $PackagePath="$ProjectPath\$Package"

    Write-Host "$PackagePath\$TargetAssembly"
    New-Item -Type Directory -Path "$PackagePath\plugins" -Force
    Copy-Item -Path "$TargetPath\$TargetAssembly" -Destination "$PackagePath\plugins\$TargetAssembly" -Force
    Copy-ModAssetBundle "$PackagePath\plugins\valheimmodassets"
    Remove-LegacyAssetBundle "$PackagePath\plugins"
    Copy-Item -Path "$ProjectPath\README.md" -Destination "$PackagePath\README.md" -Force
    Compress-Archive -Path "$PackagePath\*" -DestinationPath "$TargetPath\$name.zip" -Force
}

# Pop Location
Pop-Location
