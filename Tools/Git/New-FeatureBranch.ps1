[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Name
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

if ($Name -notmatch '^[a-z0-9][a-z0-9-]*$') {
    throw "分支名只能使用小写字母、数字和连字符，例如 player-movement。"
}

$changes = git -C $projectRoot status --porcelain
if ($changes) {
    throw "工作区有未提交修改。请先提交或暂存，再创建分支。"
}

git -C $projectRoot fetch origin --prune
git -C $projectRoot switch main
git -C $projectRoot pull --ff-only origin main
git -C $projectRoot switch -c "feature/$Name"

Write-Host "已创建分支 feature/$Name。完成后执行 git push -u origin feature/$Name。" -ForegroundColor Green
