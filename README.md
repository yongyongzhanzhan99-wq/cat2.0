# Cat2.0 Unity 项目协作说明

本仓库使用 Unity 2022.3.62f3。请通过 Unity Hub 打开本目录，而不是 `Library` 等子目录。

## 需要提交

- `Assets/`（包括所有 `.meta` 文件）
- `Packages/`
- `ProjectSettings/`
- `.gitignore`、`.gitattributes` 和 `Tools/Git/`

## 不要提交

`.gitignore` 已排除 `Library/`、`Logs/`、`Temp/`、`Obj/`、`Build/`、`UserSettings/` 和 IDE 生成文件。它们会在本机重新生成。

## 日常流程

1. 关闭 Unity，或确保没有未保存修改。
2. 运行 `Tools/Git/Update-Project.ps1` 更新 `main`。
3. 运行 `Tools/Git/New-FeatureBranch.ps1 -Name player-movement` 创建个人功能分支。
4. 在 Unity 中工作，连同变更资源的 `.meta` 文件一起提交。
5. 推送功能分支并创建 Pull Request，合并前避免多人同时修改同一个场景或 Prefab。

Git LFS 已用于常见的大型美术、模型、音频和视频格式。新成员克隆后运行 `git lfs install`，再打开项目。
