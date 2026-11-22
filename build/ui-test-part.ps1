<#
.SYNOPSIS
  Morph: the dotnet test filter for one part of the UI tests (tests/Filee.App.Tests), so CI can run them on several
  machines at once.

.DESCRIPTION
  Finds the test classes (classes in files with [Fact], [Theory], [AvaloniaFact] or [AvaloniaTheory]) and shares
  them out by the size of their files, biggest first, each to the part with the least so far (a rough measure of
  how long they take). Parts 1 to N-1 get a filter naming their classes; part N gets everything that isn't in the
  others, so a test class this script misses (or a new one) still runs, in the last part.

.EXAMPLE
  $filter = ./build/ui-test-part.ps1 -Part 2 -Parts 4
  dotnet test tests/Filee.App.Tests --no-build -c Release --filter $filter
#>
param(
    [Parameter(Mandatory)][int]$Part,
    [int]$Parts = 4,
    [string]$TestsDir = (Join-Path $PSScriptRoot '..\tests\Filee.App.Tests')
)
$ErrorActionPreference = 'Stop'
if ($Part -lt 1 -or $Part -gt $Parts) { throw "Part must be 1 to $Parts" }

# Class name -> size (a partial class in several files adds up). Only top-level classes of the test namespace.
$sizes = @{}
foreach ($file in Get-ChildItem $TestsDir -Filter *.cs -File) {
    $text = Get-Content $file.FullName -Raw -Encoding utf8
    if ($text -notmatch '\[\w*(Fact|Theory)\b') { continue }
    if ($text -notmatch '(?m)^namespace\s+Filee\.App\.Tests\s*;') { continue }
    foreach ($m in [regex]::Matches($text, '(?m)^(?:public|internal)\s+(?:(?:sealed|static|partial|abstract)\s+)*class\s+(\w+)')) {
        $name = $m.Groups[1].Value
        $sizes[$name] = ($sizes[$name] ?? 0) + $text.Length
    }
}
if ($sizes.Count -lt $Parts) { throw "Only $($sizes.Count) test classes found in $TestsDir" }

$loads = [long[]]::new($Parts - 1)
$members = @(1..($Parts - 1) | ForEach-Object { [System.Collections.Generic.List[string]]::new() })
# The last part gets a share too (by leaving it what's left): weigh it as one more bin.
$lastLoad = 0L
foreach ($entry in $sizes.GetEnumerator() | Sort-Object { -$_.Value }, Key) {
    $min = 0
    for ($i = 1; $i -lt $loads.Length; $i++) { if ($loads[$i] -lt $loads[$min]) { $min = $i } }
    if ($lastLoad -lt $loads[$min]) { $lastLoad += $entry.Value; continue }
    $loads[$min] += $entry.Value
    $members[$min].Add($entry.Key)
}

function Clause([string]$name, [string]$op) { "FullyQualifiedName$($op)Filee.App.Tests.$name." }
if ($Part -lt $Parts) {
    ($members[$Part - 1] | Sort-Object | ForEach-Object { Clause $_ '~' }) -join '|'
}
else {
    ($members | ForEach-Object { $_ } | Sort-Object | ForEach-Object { Clause $_ '!~' }) -join '&'
}
