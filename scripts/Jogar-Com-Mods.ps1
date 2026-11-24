param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$PastaDoJogo
)
$ErrorActionPreference = 'Stop'
$game = (Get-Item -LiteralPath $PastaDoJogo).FullName
foreach ($file in 'default.xex','xarchive_cache.rpf','xarchive_audio.rpf','xarchive_music.rpf','xarchive_audlo.rpf') {
    if (-not (Test-Path -LiteralPath (Join-Path $game $file) -PathType Leaf)) {
        throw "A pasta do mod precisa conter os dados completos do jogo. Falta $file."
    }
}
$exe = Join-Path $PSScriptRoot 'mcla.exe'
if (Get-Process mcla -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    throw 'Feche o jogo antes de iniciar com mods.'
}
$translated = (Test-Path -LiteralPath (Join-Path $game '.mcla-ptbr.json')).ToString().ToLowerInvariant()
# Mod sessions have a separate profile and cache from the regular campaign.
$profile = Join-Path $PSScriptRoot 'userdata-mods'
Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -ArgumentList @(
    ('--game_data_root="{0}"' -f $game)
    ('--user_data_root="{0}"' -f $profile)
    "--mcla_ptbr=$translated"
)
