[CmdletBinding()]
param(
    [string]$WidgetsRoot = (Join-Path $PSScriptRoot '..\widgets'),

    [string]$WidgetName
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $WidgetsRoot).Path
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$widgetDirs = if ($WidgetName) {
    $selectedWidget = Join-Path $resolvedRoot $WidgetName
    if (-not (Test-Path -LiteralPath $selectedWidget -PathType Container)) {
        throw "Widget directory does not exist: $selectedWidget"
    }
    @((Get-Item -LiteralPath $selectedWidget))
} else {
    @(Get-ChildItem -LiteralPath $resolvedRoot -Directory)
}

if ($widgetDirs.Count -eq 0) {
    throw "No widget directories found in: $resolvedRoot"
}

foreach ($widgetDir in $widgetDirs) {
    $widgetName = $widgetDir.Name
    $entryFile = Join-Path $widgetDir.FullName ($widgetName + '.qml')

    if (-not (Test-Path -LiteralPath $entryFile -PathType Leaf)) {
        $errors.Add("[$widgetName] Missing entry file: $widgetName.qml")
        continue
    }

    $source = Get-Content -LiteralPath $entryFile -Raw
    $escapedName = [regex]::Escape($widgetName)

    if ($source -notmatch ('property\s+string\s+path\s*:\s*["'']' + $escapedName + '["'']')) {
        $errors.Add("[$widgetName] path property must exactly match the directory name")
    }
    if ($source -notmatch 'function\s+get_widget_ctx\s*\(') {
        $errors.Add("[$widgetName] Missing get_widget_ctx()")
    }
    if ($source -notmatch 'function\s+set_widget_ctx\s*\(') {
        $errors.Add("[$widgetName] Missing set_widget_ctx()")
    }
    if ($source -notmatch 'import\s+MyModules\s+1\.0') {
        $warnings.Add("[$widgetName] MyModules 1.0 is not imported")
    }

    foreach ($match in [regex]::Matches($source, 'import\s+\S+\s+(\d+)\.(\d+)')) {
        $major = [int]$match.Groups[1].Value
        $minor = [int]$match.Groups[2].Value
        if (($major -eq 2 -and $minor -gt 14) -or ($major -gt 2)) {
            $warnings.Add("[$widgetName] Import may exceed Qt 5.14.2 compatibility: $($match.Value)")
        }
    }
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) {
        Write-Error $validationError
    }
    exit 1
}

Write-Host "Validated $($widgetDirs.Count) widget(s) successfully." -ForegroundColor Green
