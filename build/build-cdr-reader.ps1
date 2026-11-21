<#
.SYNOPSIS
  Builds MorphCdr.exe, Morph's CorelDRAW reader (src/Morph.CdrReader), for Windows x64 or ARM64.
.DESCRIPTION
  Adapted from OrbitDrop's build/cdr/build-cdr.ps1 (MIT, github.com/i3sam/OrbitDrop). Downloads the exact upstream
  source releases below (each checked against its SHA-256, so the build is always the same code) and compiles them
  with the reader into one self-contained exe (static C runtime; ICU comes from Windows). Nothing is installed.
  Sources are cached in build\.cache\cdr-src, the built reader in build\.cache\cdr-<architecture> (rebuilt when the
  reader's source or this script changes). Then MorphCdr.exe and the libraries' licences are copied to -Destination.

  libcdr 0.1.9, librevenge 0.0.6  MPL 2.0          The Document Foundation (librevenge is used under its MPL option)
  lcms2 2.16                      MIT              Marti Maria
  zlib 1.3.1                      zlib licence     Jean-loup Gailly, Mark Adler
  Boost 1.86 (headers only)       Boost licence    Boost contributors
  The sources are used unmodified.
.EXAMPLE
  pwsh build/build-cdr-reader.ps1                                   # engines\cdr, where the tests find it
  pwsh build/build-cdr-reader.ps1 -Destination publish/win-arm64/engines/cdr -Architecture arm64
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\engines\cdr'),
    [ValidateSet('x64', 'arm64')][string]$Architecture = 'x64'
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$cache = Join-Path $PSScriptRoot '.cache'
$sourcesDir = Join-Path $cache 'cdr-src'
$built = Join-Path $cache "cdr-$Architecture"
$src = Join-Path $root 'src\Morph.CdrReader'

# The built reader is reused while its source and this script are the same.
$stamp = (Get-FileHash (Join-Path $src 'MorphCdr.cpp')).Hash + (Get-FileHash $PSCommandPath).Hash
$stampFile = Join-Path $built 'stamp.txt'
$exe = Join-Path $built 'MorphCdr.exe'
if (-not ((Test-Path $exe) -and (Test-Path $stampFile) -and (Get-Content $stampFile -Raw).Trim() -eq $stamp)) {
    New-Item -ItemType Directory -Force $sourcesDir, $built | Out-Null
    $sources = @(
        @{ Url = 'https://dev-www.libreoffice.org/src/libcdr/libcdr-0.1.9.tar.xz'; Sha = 'f7bb6abdd7f226820f288a93dd8d07759833c0250d9e202af90f9b312c4665a3'; Dir = 'libcdr-0.1.9' },
        @{ Url = 'https://dev-www.libreoffice.org/src/librevenge-0.0.6.tar.bz2'; Sha = '52a65e904d255dbdd97a8b7bb28d6574e14f999eb01416aff004502406d0904d'; Dir = 'librevenge-0.0.6' },
        @{ Url = 'https://github.com/mm2/Little-CMS/releases/download/lcms2.16/lcms2-2.16.tar.gz'; Sha = 'd873d34ad8b9b4cea010631f1a6228d2087475e4dc5e763eb81acc23d9d45a51'; Dir = 'lcms2-2.16' },
        @{ Url = 'https://github.com/madler/zlib/releases/download/v1.3.1/zlib-1.3.1.tar.gz'; Sha = '9a93b2b7dfdac77ceba5a558a580e74667dd6fede4585b91eefb60f03b72df23'; Dir = 'zlib-1.3.1' },
        @{ Url = 'https://archives.boost.io/release/1.86.0/source/boost_1_86_0.tar.bz2'; Sha = '1bed88e40401b2cb7a1f76d4bab499e352fa4d0c5f31c0dbae64e24d34d7513b'; Dir = 'boost_1_86_0'; Only = @('boost_1_86_0/boost', 'boost_1_86_0/LICENSE_1_0.txt') }
    )
    foreach ($s in $sources) {
        $dir = Join-Path $sourcesDir $s.Dir
        if (Test-Path $dir) { continue }
        $archive = Join-Path $sourcesDir ([IO.Path]::GetFileName($s.Url))
        if (-not (Test-Path $archive)) { Write-Host "  downloading $($s.Url)"; Invoke-WebRequest $s.Url -OutFile $archive -UseBasicParsing }
        $sha = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sha -ne $s.Sha) { Remove-Item $archive; throw "Checksum mismatch for $($s.Url): got $sha" }
        $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
        if ($s.Only) { & $tar -xf $archive -C $sourcesDir @($s.Only) } else { & $tar -xf $archive -C $sourcesDir }
        if ($LASTEXITCODE -ne 0) { throw "Couldn't unpack $archive" }
        Remove-Item $archive
    }

    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    $vswhere = Join-Path $installer 'vswhere.exe'
    if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found: install Visual Studio (or Build Tools) with C++.' }
    $component = if ($Architecture -eq 'arm64') { 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' } else { 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' }
    $vs = & $vswhere -latest -products * -requires $component -property installationPath
    if (-not $vs) { throw "No Visual C++ build tools for $Architecture found." }
    $native = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $vcvars = Join-Path $vs ('VC\Auxiliary\Build\' + $(if ($Architecture -eq 'x64') { 'vcvars64.bat' } elseif ($native -eq 'arm64') { 'vcvarsarm64.bat' } else { 'vcvarsamd64_arm64.bat' }))
    $env:PATH = "$installer;$env:PATH"

    $cdr = Join-Path $sourcesDir 'libcdr-0.1.9'; $rvng = Join-Path $sourcesDir 'librevenge-0.0.6'
    $lcms = Join-Path $sourcesDir 'lcms2-2.16'; $zlib = Join-Path $sourcesDir 'zlib-1.3.1'; $boost = Join-Path $sourcesDir 'boost_1_86_0'
    $obj = Join-Path $built 'obj'
    if (Test-Path $obj) { Remove-Item $obj -Recurse -Force }
    New-Item -ItemType Directory -Force $obj | Out-Null

    # librevenge's "folder as a document" stream (POSIX-only, and never used for drawings) is left out.
    $cpp = @(Get-ChildItem (Join-Path $cdr 'src\lib') -Filter *.cpp) +
        @(Get-ChildItem (Join-Path $rvng 'src\lib') -Filter *.cpp | Where-Object Name -ne 'RVNGDirectoryStream.cpp') +
        @(Get-Item (Join-Path $src 'MorphCdr.cpp'))
    $c = @(Get-ChildItem (Join-Path $lcms 'src') -Filter *.c) + @(Get-ChildItem $zlib -Filter *.c)
    $includes = @("$src\icu-shim", "$cdr\inc", "$cdr\src\lib", "$rvng\inc", "$rvng\src\lib", "$lcms\include", $zlib, $boost) | ForEach-Object { "/I""$_""" }
    $defines = '/DWIN32 /D_WINDOWS /DNDEBUG /DNOMINMAX /D_USE_MATH_DEFINES /DBOOST_ALL_NO_LIB /D_CRT_SECURE_NO_WARNINGS /D_CRT_NONSTDC_NO_DEPRECATE /D_SCL_SECURE_NO_WARNINGS'
    $common = "/nologo /c /O2 /MT /MP /W0 /bigobj /utf-8 $defines $($includes -join ' ') /Fo""$obj\\"""

    # Response files keep the command lines short.
    $cppRsp = Join-Path $obj 'cpp.rsp'; $cRsp = Join-Path $obj 'c.rsp'; $linkRsp = Join-Path $obj 'link.rsp'
    Set-Content $cppRsp -Encoding ASCII -Value ("$common /EHsc /std:c++17 " + (($cpp | ForEach-Object { """$($_.FullName)""" }) -join ' '))
    Set-Content $cRsp -Encoding ASCII -Value ("$common /TC " + (($c | ForEach-Object { """$($_.FullName)""" }) -join ' '))

    Write-Host "  compiling $($cpp.Count + $c.Count) files ($Architecture)"
    cmd /c """$vcvars"" >nul && cl @""$cRsp"" >""$obj\c.log"" 2>&1 && cl @""$cppRsp"" >""$obj\cpp.log"" 2>&1"
    if ($LASTEXITCODE -ne 0) { Get-Content "$obj\c.log", "$obj\cpp.log" -ErrorAction SilentlyContinue | Select-String 'error' | Select-Object -First 20 | ForEach-Object { Write-Host $_ }; throw 'Compiling the CorelDRAW reader failed.' }

    $objs = Get-ChildItem $obj -Filter *.obj | ForEach-Object { """$($_.FullName)""" }
    Set-Content $linkRsp -Encoding ASCII -Value ("/nologo /SUBSYSTEM:CONSOLE /OPT:REF /OPT:ICF /OUT:""$exe"" " + ($objs -join ' ') + ' icu.lib')
    cmd /c """$vcvars"" >nul && link @""$linkRsp"" >""$obj\link.log"" 2>&1"
    if ($LASTEXITCODE -ne 0) { Get-Content "$obj\link.log" | Select-Object -First 20 | ForEach-Object { Write-Host $_ }; throw 'Linking the CorelDRAW reader failed.' }
    Remove-Item $obj -Recurse -Force

    # Each library's own licence travels with the reader (MIT, zlib and Boost ask for it; MPL 2.0's source is upstream, unmodified).
    $licenses = Join-Path $built 'licenses'
    New-Item -ItemType Directory -Force $licenses | Out-Null
    Copy-Item (Join-Path $cdr 'COPYING.MPL') (Join-Path $licenses 'libcdr-0.1.9 (MPL-2.0).txt')
    Copy-Item (Join-Path $rvng 'COPYING.MPL') (Join-Path $licenses 'librevenge-0.0.6 (MPL-2.0).txt')
    Copy-Item (Join-Path $lcms 'LICENSE') (Join-Path $licenses 'lcms2-2.16 (MIT).txt')
    Copy-Item (Join-Path $zlib 'LICENSE') (Join-Path $licenses 'zlib-1.3.1 (zlib).txt')
    Copy-Item (Join-Path $boost 'LICENSE_1_0.txt') (Join-Path $licenses 'Boost-1.86 (BSL-1.0).txt')
    Set-Content $stampFile $stamp -NoNewline
    Write-Host "Built $exe"
}

New-Item -ItemType Directory -Force $Destination | Out-Null
Copy-Item $exe $Destination -Force
Copy-Item (Join-Path $built 'licenses') $Destination -Recurse -Force
Write-Host "MorphCdr.exe -> $Destination"
