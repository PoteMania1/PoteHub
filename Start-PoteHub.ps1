$ErrorActionPreference = "Stop"

$root = $PSScriptRoot

$trackerPath = Join-Path `
    $root `
    "publish\Tracker\PoteHub.Tracker.exe"

$discordPath = Join-Path `
    $root `
    "publish\Discord\PoteHub.Discord.exe"

$logsPath = Join-Path `
    $env:LOCALAPPDATA `
    "PoteHub\runner-logs"

New-Item `
    -ItemType Directory `
    -Path $logsPath `
    -Force | Out-Null

function Start-PoteHubProcess {
    param(
        [string]$Name,
        [string]$Path
    )

    $outputPath = Join-Path `
        $logsPath `
        "$Name-output.log"

    $errorPath = Join-Path `
        $logsPath `
        "$Name-error.log"

    return Start-Process `
        -FilePath $Path `
        -WorkingDirectory (Split-Path $Path) `
        -RedirectStandardOutput $outputPath `
        -RedirectStandardError $errorPath `
        -WindowStyle Hidden `
        -PassThru
}

$tracker = $null
$discord = $null

try {
    while ($true) {
        if (
            $null -eq $tracker -or
            $tracker.HasExited
        ) {
            $tracker = Start-PoteHubProcess `
                -Name "tracker" `
                -Path $trackerPath
        }

        if (
            $null -eq $discord -or
            $discord.HasExited
        ) {
            $discord = Start-PoteHubProcess `
                -Name "discord" `
                -Path $discordPath
        }

        Start-Sleep -Seconds 5
    }
}
finally {
    if (
        $null -ne $tracker -and
        -not $tracker.HasExited
    ) {
        Stop-Process `
            -Id $tracker.Id
    }

    if (
        $null -ne $discord -and
        -not $discord.HasExited
    ) {
        Stop-Process `
            -Id $discord.Id
    }
}