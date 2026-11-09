# FitnessTimer 健身间歇计时器

[English README](README.en.md)

一个轻量 Windows 桌面间歇训练计时器：WPF / C# / .NET 8，单窗口、无浏览器无本地服务，数据全部存本地 JSON。

## 功能

- **训练方案编辑**：多个动作分组，每组独立设置训练时长与休息时长；支持添加、删除、上移、下移，自动保存
- **计时流程**：准备倒计时 → 训练 → 休息循环，支持暂停 / 继续 / 停止 / 跳过当前阶段
- **紧凑模式**：只保留倒计时、动作名、阶段、组数的小窗，可置顶；记住上次关闭时的窗口模式
- **历史记录**：每次训练自动落盘（时长、完成组数、跳过情况）
- **统计图表**：周 / 月柱状图，WPF 自绘，无第三方图表依赖
- **深色模式**、窗口位置记忆

## 下载

到 [Releases](https://github.com/daze0515/FitnessTimer/releases) 下载 zip，解压后运行 `FitnessTimer.App.exe`（需要 .NET 8 桌面运行时，[下载地址](https://dotnet.microsoft.com/download/dotnet/8.0)）。

## 从源码构建

需要 .NET 8 SDK（Windows）。

```bat
scripts\run-app.cmd    :: 构建并运行
scripts\test.cmd       :: 运行全部单元测试
```

或直接 `dotnet build FitnessTimer.sln` / `dotnet test FitnessTimer.sln`。

## 目录结构

```
FitnessTimer.App/       WPF 应用（Models / Services / ViewModels / Views）
FitnessTimer.Tests/     MSTest 单元测试
scripts/                构建 / 测试脚本
```

运行时数据（训练方案、历史、设置）写在程序同级的 `data/` 目录，首次启动自动生成默认数据。

## License

[MIT](LICENSE)
