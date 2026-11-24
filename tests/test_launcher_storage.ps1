$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('mcla-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $scratch | Out-Null
try {
    $exe=Join-Path $scratch 'LauncherStorageTests.exe'
    $testSource=Join-Path $PSScriptRoot 'LauncherStorageTests.cs'
    $saveSource=Join-Path $project 'installer\source\SaveBackup.cs'
    $configSource=Join-Path $project 'installer\source\LauncherSettings.cs'
    $source=Join-Path $project 'installer\source'
    $framework=Split-Path $compiler -Parent
    $options=@('/nologo','/main:LauncherStorageTests','/reference:System.Core.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',
      "/reference:$framework\System.Xaml.dll","/reference:$framework\WPF\WindowsBase.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\PresentationFramework.dll",
      "/resource:$source\Launcher.xaml,MclaSetup.Launcher.xaml","/resource:$project\config\graphics_profiles.json,MclaSetup.graphics_profiles.json","/resource:$source\assets\banner.png,MclaSetup.banner.png","/resource:$source\assets\cover.png,MclaSetup.cover.png","/resource:$source\assets\logo-recompiled.png,MclaSetup.logo.png","/resource:$source\assets\setup.ico,MclaSetup.setup.ico",
      "/out:$exe",$testSource,$saveSource,$configSource,"$source\UpdateChecker.cs","$source\Launcher.cs","$source\Core.cs")
    & $compiler @options
    if($LASTEXITCODE -ne 0){throw 'Compilação dos testes falhou.'}
    & $exe
    if($LASTEXITCODE -ne 0){throw 'Testes de armazenamento falharam.'}
} finally {
    # This is the absolute, freshly created test directory, never player data.
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
