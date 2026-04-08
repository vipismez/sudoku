# SudokuApp

一个基于 C#、.NET 8 和 WPF 的 9x9 数独桌面程序，当前版本聚焦于纯手动输入、规则校验、求解和结果展示。

## 当前功能

- 9x9 数独手动输入
- 输入阶段实时冲突高亮
- 基于标准数独规则的题盘校验
- 数独自动求解
- 结果展示区区分原题数字与补全数字
- 多解题提示
- 本地日志记录

## 当前不包含的功能

- 图片上传
- OCR 识别

这两项已经从当前版本中移除，项目现在只保留手动录入数独的主流程。

## 开发环境

- Windows
- .NET SDK 8.0+
- WPF

如果本机没有把 dotnet 加入 PATH，可以直接使用完整路径运行，例如：

```powershell
C:\Users\vipis\.dotnet\dotnet.exe build .\SudokuApp.sln -c Release
```

## 运行方式

在项目根目录执行：

```powershell
C:\Users\vipis\.dotnet\dotnet.exe run --project .\SudokuApp\SudokuApp.csproj
```

也可以直接打开 [SudokuApp.sln](SudokuApp.sln) 用 Visual Studio 运行。

## 项目结构

- [SudokuApp/MainWindow.xaml](SudokuApp/MainWindow.xaml)：主界面布局
- [SudokuApp/MainWindow.xaml.cs](SudokuApp/MainWindow.xaml.cs)：输入交互、冲突高亮、求解流程
- [SudokuApp/SudokuSolver.cs](SudokuApp/SudokuSolver.cs)：回溯求解器与多解判定
- [SudokuApp/SudokuValidator.cs](SudokuApp/SudokuValidator.cs)：数独规则校验
- [SudokuApp/AppLogger.cs](SudokuApp/AppLogger.cs)：日志记录
- [SudokuApp/App.xaml.cs](SudokuApp/App.xaml.cs)：应用入口与全局异常处理

## 使用说明

1. 在左侧题盘中输入已知数字。
2. 如果输入造成行、列或 3x3 宫冲突，相关格子会即时高亮。
3. 点击“求解”后，程序会先校验题盘，再进行求解。
4. 只有结果通过完整规则校验后，才会在右侧答案区展示。

## 日志位置

运行日志默认写入：

```text
C:\Users\vipis\AppData\Local\SudokuApp\logs\app.log
```

## 说明

- 求解算法采用回溯搜索，并优先选择候选数最少的空格，以减少搜索分支。
- 结果展示前会再次执行完整规则校验，避免错误结果被展示。