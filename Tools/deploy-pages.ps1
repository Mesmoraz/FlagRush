# Publish Builds/Web to the gh-pages branch as a single fresh commit (no history, so main stays small).
# Usage: .\Tools\deploy-pages.ps1   (run after a release web build)
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'Builds\Web'
if (-not (Test-Path (Join-Path $build 'index.html'))) { throw "No web build at $build - run SpikeBuilder.BuildWebGl2 first." }

function Run($cmd) {
    # git writes progress/warnings to stderr; only the exit code means failure.
    $out = & cmd /c "$cmd 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "failed ($LASTEXITCODE): $cmd`n$out" }
    $out
}

$remote = (Run "git -C `"$root`" remote get-url origin") | Select-Object -Last 1
$sha = (Run "git -C `"$root`" rev-parse --short HEAD") | Select-Object -Last 1
$name = (Run "git -C `"$root`" config user.name") | Select-Object -Last 1
$email = (Run "git -C `"$root`" config user.email") | Select-Object -Last 1
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("flagrush-pages-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    Copy-Item (Join-Path $build '*') $tmp -Recurse
    New-Item -ItemType File (Join-Path $tmp '.nojekyll') | Out-Null   # Jekyll would drop files starting with _
    Run "git -C `"$tmp`" init -q -b gh-pages" | Out-Null
    Run "git -C `"$tmp`" -c core.autocrlf=false add -A" | Out-Null
    Run "git -C `"$tmp`" -c user.name=`"$name`" -c user.email=`"$email`" commit -q -m `"Web build from $sha`"" | Out-Null
    Run "git -C `"$tmp`" push --force `"$remote`" gh-pages:gh-pages" | Out-Null
    Write-Host "Published Builds/Web (from $sha) to gh-pages."
} finally {
    Remove-Item $tmp -Recurse -Force
}
