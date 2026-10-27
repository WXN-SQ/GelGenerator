[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')]
    [string]$WidgetName,

    [string]$WidgetsRoot = (Join-Path $PSScriptRoot '..\widgets'),

    [string]$DestinationRoot = 'C:\Program Files\Gutega\VOFA+\x64\plugins\widgets',

    [switch]$Replace
)

$ErrorActionPreference = 'Stop'
$source = Join-Path (Resolve-Path -LiteralPath $WidgetsRoot).Path $WidgetName
$entry = Join-Path $source ($WidgetName + '.qml')

if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Widget directory does not exist: $source"
}
if (-not (Test-Path -LiteralPath $entry -PathType Leaf)) {
    throw "Widget entry file does not exist: $entry"
}
if (-not (Test-Path -LiteralPath $DestinationRoot -PathType Container)) {
    throw "VOFA+ widgets directory does not exist: $DestinationRoot"
}

$destination = Join-Path $DestinationRoot $WidgetName
if ((Test-Path -LiteralPath $destination) -and -not $Replace) {
    throw "Destination already exists. Use -Replace to update it: $destination"
}

if ($PSCmdlet.ShouldProcess($destination, "Deploy widget from $source")) {
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
    Write-Host "Deployed: $destination" -ForegroundColor Green
    Write-Host 'Restart VOFA+ and refresh the widget list.'
}
