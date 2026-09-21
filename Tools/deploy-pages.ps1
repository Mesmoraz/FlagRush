# Publish Builds/Web to the gh-pages branch as a single fresh commit (no history, so main stays small).
# Usage: .\Tools\deploy-pages.ps1   (run after a release web build)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'Builds\Web'
if (-not (Test-Path (Join-Path $build 'index.html'))) { throw "No web build at $build - run SpikeBuilder.BuildWeb first." }

$remote = git -C $root remote get-url origin
$sha = git -C $root rev-parse --short HEAD
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("flagrush-pages-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null
try {
    Copy-Item (Join-Path $build '*') $tmp -Recurse
    New-Item -ItemType File (Join-Path $tmp '.nojekyll') | Out-Null   # Jekyll would drop files starting with _
    git -C $tmp init -q -b gh-pages
    git -C $tmp add -A
    git -C $tmp -c user.name="$(git -C $root config user.name)" -c user.email="$(git -C $root config user.email)" commit -q -m "Web build from $sha"
    git -C $tmp push --force $remote gh-pages:gh-pages
    Write-Host "Published Builds/Web (from $sha) to gh-pages."
} finally {
    Remove-Item $tmp -Recurse -Force
}
