[CmdletBinding()]
param(
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

if (-not (Test-Path (Join-Path $projectRoot ".git"))) {
    throw "未在 Unity 项目根目录找到 Git 仓库。"
}

$changes = git -C $projectRoot status --porcelain
if ($changes) {
    throw "工作区有未提交修改。请先提交或暂存，再更新项目。"
}

git -C $projectRoot fetch origin --prune
git -C $projectRoot switch $Branch
git -C $projectRoot pull --ff-only origin $Branch
git -C $projectRoot lfs pull

Write-Host "项目已安全更新到 origin/$Branch。现在可重新打开或回到 Unity。" -ForegroundColor Green
