# SudokuApp

一个基于 C#、.NET 8 和 WPF 的 9x9 数独桌面程序。
当前版本聚焦于“手动录入 + 难度出题 + 动态演示求解过程 + 结果校验”的完整闭环。

## 功能概览

- 单棋盘模式：输入、演示、结果都在同一棋盘中完成
- 手动输入与即时冲突高亮（行、列、3x3 宫）
- 快速出题：容易 / 中等 / 困难
- 求解过程播放控制：暂停、继续、停止、重新开始、上一步、下一步、直接完成
- 过程统计：线索数、空格数、预计步数、当前步进度
- 模式提示：编辑模式 / 演示模式 / 暂停模式 / 结果模式 / 完成模式
- 多解检测提示（展示其中一个有效解）
- 本地日志记录

## 不包含的功能

- 图片上传
- OCR 识别

## 技术栈

- Windows
- .NET SDK 8.0+
- WPF

## 构建与运行

在仓库根目录执行：

```powershell
C:\Users\vipis\.dotnet\dotnet.exe build .\SudokuApp.sln -c Release
```

```powershell
C:\Users\vipis\.dotnet\dotnet.exe run --project .\SudokuApp\SudokuApp.csproj
```

也可以直接打开 [SudokuApp.sln](SudokuApp.sln) 用 Visual Studio 运行。

## 使用流程

1. 手动输入题目，或点击“容易 / 中等 / 困难”生成新题。
2. 检查左侧过程信息中的题目统计与预计演示步数。
3. 点击“求解”开始自动演示；可随时暂停并手动步进。
4. 演示结束后查看最终结果与状态提示。

## 求解思路（简述）

- 先做输入合法性校验，避免无效盘面进入搜索。
- 使用 MRV（最少候选数优先）回溯算法求解。
- 同步记录 Fill / Backtrack 步骤，用于动画回放与手动步进。
- 求解完成后做完整规则校验，并给出多解提示。

## 项目结构

- [SudokuApp/MainWindow.xaml](SudokuApp/MainWindow.xaml)：主界面布局与控件命名
- [SudokuApp/MainWindow.xaml.cs](SudokuApp/MainWindow.xaml.cs)：输入交互、播放状态机、UI 刷新逻辑
- [SudokuApp/SudokuSolver.cs](SudokuApp/SudokuSolver.cs)：MRV 回溯求解、步骤记录、多解判定
- [SudokuApp/SudokuGenerator.cs](SudokuApp/SudokuGenerator.cs)：分档出题与唯一解约束
- [SudokuApp/SudokuValidator.cs](SudokuApp/SudokuValidator.cs)：局部/完整数独规则校验
- [SudokuApp/AppLogger.cs](SudokuApp/AppLogger.cs)：日志记录
- [SudokuApp/App.xaml.cs](SudokuApp/App.xaml.cs)：应用入口与全局异常处理

## 日志位置

默认写入：

```text
C:\Users\vipis\AppData\Local\SudokuApp\logs\app.log
```