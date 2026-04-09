using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SudokuApp;

/// <summary>
/// 主界面负责手动输入数独、冲突提示、出题、求解过程控制与答案展示。
/// </summary>
public partial class MainWindow : Window
{
    private readonly record struct CellPosition(int Row, int Col);
    private const int SolveAnimationDelayMilliseconds = 500;

    private readonly TextBox[,] _inputBoxes = new TextBox[9, 9];
    private readonly DispatcherTimer _solveTimer;

    // 播放状态相关数据：起始盘、当前显示盘、最终解和步骤游标。
    private int[,] _lastInput = new int[9, 9];
    private int[,] _playbackStartBoard = new int[9, 9];
    private int[,] _playbackDisplayBoard = new int[9, 9];
    private int[,] _playbackSolvedBoard = new int[9, 9];
    private List<SudokuSolveStep> _playbackSteps = new();
    private int _currentStepIndex;
    private bool _playbackLoaded;
    private bool _playbackPaused;
    private bool _playbackCompleted;
    private bool _playbackHasMultipleSolutions;
    private string _currentPuzzleDifficultyText = "手动输入";
    private bool _isBoardUpdating;
    private bool _isBoardEditable = true;

    public MainWindow()
    {
        InitializeComponent();
        BuildInputGrid();

        _solveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SolveAnimationDelayMilliseconds)
        };
        _solveTimer.Tick += SolveTimerOnTick;

        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
        UpdateBoardModeIndicator();
        AppLogger.Info("主窗口初始化完成");
    }

    /// <summary>
    /// 动态构建 9x9 输入格，并绑定输入过滤、焦点移动与冲突刷新事件。
    /// </summary>
    private void BuildInputGrid()
    {
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var box = new TextBox
                {
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                    MaxLength = 1,
                    BorderBrush = CreateCellBorderBrush(),
                    BorderThickness = GetCellBorderThickness(row, col),
                    Background = CreateInputCellBackground(row, col),
                    Foreground = new SolidColorBrush(Color.FromRgb(41, 57, 52)),
                    CaretBrush = new SolidColorBrush(Color.FromRgb(41, 57, 52)),
                    Tag = new CellPosition(row, col)
                };
                box.PreviewTextInput += InputBoxOnPreviewTextInput;
                box.TextChanged += InputBoxOnTextChanged;
                box.PreviewKeyDown += InputBoxOnPreviewKeyDown;
                _inputBoxes[row, col] = box;
                InputGrid.Children.Add(box);
            }
        }
    }

    private static SolidColorBrush CreateCellBorderBrush()
    {
        return new SolidColorBrush(Color.FromRgb(118, 128, 120));
    }

    private static Thickness GetCellBorderThickness(int row, int col)
    {
        const double thin = 0.8;
        const double thick = 2.6;

        // 每个 3x3 宫的边界使用粗线，普通单元格之间使用细线。
        var left = col == 0 || col % 3 == 0 ? thick : thin;
        var top = row == 0 || row % 3 == 0 ? thick : thin;
        var right = col == 8 ? thick : 0;
        var bottom = row == 8 ? thick : 0;

        return new Thickness(left, top, right, bottom);
    }

    private static bool IsWarmBox(int row, int col)
    {
        return ((row / 3) + (col / 3)) % 2 == 0;
    }

    private static SolidColorBrush CreateInputCellBackground(int row, int col)
    {
        return IsWarmBox(row, col)
            ? new SolidColorBrush(Color.FromRgb(252, 247, 236))
            : new SolidColorBrush(Color.FromRgb(239, 247, 241));
    }

    private static SolidColorBrush CreateConflictBackground(int row, int col)
    {
        return IsWarmBox(row, col)
            ? new SolidColorBrush(Color.FromRgb(255, 232, 228))
            : new SolidColorBrush(Color.FromRgb(255, 237, 233));
    }

    private static SolidColorBrush CreateDisplayCellBackground(int row, int col, bool isOriginal)
    {
        if (isOriginal)
        {
            return IsWarmBox(row, col)
                ? new SolidColorBrush(Color.FromRgb(247, 240, 224))
                : new SolidColorBrush(Color.FromRgb(228, 241, 231));
        }

        return IsWarmBox(row, col)
            ? new SolidColorBrush(Color.FromRgb(255, 244, 227))
            : new SolidColorBrush(Color.FromRgb(234, 248, 238));
    }

    private static SolidColorBrush CreateFillHighlightBackground(int row, int col)
    {
        return IsWarmBox(row, col)
            ? new SolidColorBrush(Color.FromRgb(255, 232, 179))
            : new SolidColorBrush(Color.FromRgb(217, 243, 199));
    }

    private static SolidColorBrush CreateBacktrackHighlightBackground(int row, int col)
    {
        return IsWarmBox(row, col)
            ? new SolidColorBrush(Color.FromRgb(255, 220, 214))
            : new SolidColorBrush(Color.FromRgb(255, 226, 226));
    }

    private static bool IsDigitOneToNine(string input)
    {
        return input.Length == 1 && input[0] is >= '1' and <= '9';
    }

    private void InputBoxOnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_isBoardEditable)
        {
            e.Handled = true;
            return;
        }

        e.Handled = !IsDigitOneToNine(e.Text);
    }

    private void InputBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isBoardEditable)
        {
            e.Handled = true;
            return;
        }

        if (sender is not TextBox box || box.Tag is not CellPosition position)
        {
            return;
        }

        // 让用户尽量不离开键盘即可完成整盘录入。
        switch (e.Key)
        {
            case Key.Left:
                MoveFocusTo(position.Row, position.Col - 1);
                e.Handled = true;
                return;
            case Key.Right:
                MoveFocusTo(position.Row, position.Col + 1);
                e.Handled = true;
                return;
            case Key.Up:
                MoveFocusTo(position.Row - 1, position.Col);
                e.Handled = true;
                return;
            case Key.Down:
                MoveFocusTo(position.Row + 1, position.Col);
                e.Handled = true;
                return;
            case Key.Back:
                if (string.IsNullOrEmpty(box.Text))
                {
                    MoveFocusToPrevious(position.Row, position.Col);
                }
                return;
            case Key.Delete:
                box.Clear();
                UpdateInputConflictHighlight();
                e.Handled = true;
                return;
        }

        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void InputBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isBoardUpdating || !_isBoardEditable)
        {
            return;
        }

        if (sender is not TextBox box)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(box.Text))
        {
            UpdateInputConflictHighlight();
            return;
        }

        if (!IsDigitOneToNine(box.Text))
        {
            box.Text = string.Empty;
            return;
        }

        box.CaretIndex = box.Text.Length;
        UpdateInputConflictHighlight();

        if (box.Tag is CellPosition position)
        {
            MoveFocusToNext(position.Row, position.Col);
        }
    }

    private void MoveFocusTo(int row, int col)
    {
        if (row is < 0 or > 8 || col is < 0 or > 8)
        {
            return;
        }

        var target = _inputBoxes[row, col];
        target.Focus();
        target.SelectAll();
    }

    private void MoveFocusToNext(int row, int col)
    {
        var flatIndex = row * 9 + col + 1;
        if (flatIndex >= 81)
        {
            return;
        }

        MoveFocusTo(flatIndex / 9, flatIndex % 9);
    }

    private void MoveFocusToPrevious(int row, int col)
    {
        var flatIndex = row * 9 + col - 1;
        if (flatIndex < 0)
        {
            return;
        }

        MoveFocusTo(flatIndex / 9, flatIndex % 9);
    }

    /// <summary>
    /// 读取当前棋盘并高亮所有行/列/宫冲突格。
    /// </summary>
    private void UpdateInputConflictHighlight()
    {
        var board = ReadInputBoard();
        var conflicts = new bool[9, 9];

        // 将行、列、宫的重复检查结果合并到同一张冲突位图中，统一刷新背景色。
        MarkRowConflicts(board, conflicts);
        MarkColumnConflicts(board, conflicts);
        MarkBoxConflicts(board, conflicts);

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var box = _inputBoxes[row, col];
                box.Background = conflicts[row, col]
                    ? CreateConflictBackground(row, col)
                    : CreateInputCellBackground(row, col);
                box.Foreground = new SolidColorBrush(Color.FromRgb(41, 57, 52));
                box.FontWeight = FontWeights.SemiBold;
            }
        }
    }

    private static void MarkRowConflicts(int[,] board, bool[,] conflicts)
    {
        for (var row = 0; row < 9; row++)
        {
            var positions = new List<int>[10];
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = new List<int>();
            }

            for (var col = 0; col < 9; col++)
            {
                var value = board[row, col];
                if (value != 0)
                {
                    positions[value].Add(col);
                }
            }

            for (var value = 1; value <= 9; value++)
            {
                if (positions[value].Count <= 1)
                {
                    continue;
                }

                foreach (var col in positions[value])
                {
                    conflicts[row, col] = true;
                }
            }
        }
    }

    private static void MarkColumnConflicts(int[,] board, bool[,] conflicts)
    {
        for (var col = 0; col < 9; col++)
        {
            var positions = new List<int>[10];
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = new List<int>();
            }

            for (var row = 0; row < 9; row++)
            {
                var value = board[row, col];
                if (value != 0)
                {
                    positions[value].Add(row);
                }
            }

            for (var value = 1; value <= 9; value++)
            {
                if (positions[value].Count <= 1)
                {
                    continue;
                }

                foreach (var row in positions[value])
                {
                    conflicts[row, col] = true;
                }
            }
        }
    }

    private static void MarkBoxConflicts(int[,] board, bool[,] conflicts)
    {
        for (var boxRow = 0; boxRow < 3; boxRow++)
        {
            for (var boxCol = 0; boxCol < 3; boxCol++)
            {
                var positions = new List<CellPosition>[10];
                for (var i = 0; i < positions.Length; i++)
                {
                    positions[i] = new List<CellPosition>();
                }

                for (var row = boxRow * 3; row < boxRow * 3 + 3; row++)
                {
                    for (var col = boxCol * 3; col < boxCol * 3 + 3; col++)
                    {
                        var value = board[row, col];
                        if (value != 0)
                        {
                            positions[value].Add(new CellPosition(row, col));
                        }
                    }
                }

                for (var value = 1; value <= 9; value++)
                {
                    if (positions[value].Count <= 1)
                    {
                        continue;
                    }

                    foreach (var position in positions[value])
                    {
                        conflicts[position.Row, position.Col] = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 将当前 UI 输入读取为 9x9 整数棋盘，空白按 0 处理。
    /// </summary>
    private int[,] ReadInputBoard()
    {
        var board = new int[9, 9];
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                board[row, col] = int.TryParse(_inputBoxes[row, col].Text, out var value) ? value : 0;
            }
        }

        return board;
    }

    private void FillInputBoard(int[,] board)
    {
        _isBoardUpdating = true;
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _inputBoxes[row, col].Text = board[row, col] == 0 ? string.Empty : board[row, col].ToString();
                _inputBoxes[row, col].Background = CreateInputCellBackground(row, col);
                _inputBoxes[row, col].Foreground = new SolidColorBrush(Color.FromRgb(41, 57, 52));
                _inputBoxes[row, col].FontWeight = FontWeights.SemiBold;
            }
        }
        _isBoardUpdating = false;

        SetBoardEditable(true);
        UpdateInputConflictHighlight();
    }

    private void FillSolvedBoard(int[,] solved, int[,] original)
    {
        RenderBoardState(solved, original, null);
    }

    /// <summary>
    /// 渲染演示/结果棋盘。
    /// original 用于区分题目给定数字和演示中填入数字，activeStep 用于高亮当前动作。
    /// </summary>
    private void RenderBoardState(int[,] board, int[,] original, SudokuSolveStep? activeStep)
    {
        _isBoardUpdating = true;
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var wasGiven = original[row, col] != 0;
                var value = board[row, col];
                var box = _inputBoxes[row, col];
                box.Text = value == 0 ? string.Empty : value.ToString();
                box.Foreground = wasGiven
                    ? new SolidColorBrush(Color.FromRgb(38, 58, 51))
                    : new SolidColorBrush(Color.FromRgb(120, 77, 33));
                box.FontWeight = wasGiven ? FontWeights.Bold : FontWeights.SemiBold;

                if (activeStep is { Row: var activeRow, Col: var activeCol } step && activeRow == row && activeCol == col)
                {
                    box.Background = step.StepType == SudokuSolveStepType.Fill
                        ? CreateFillHighlightBackground(row, col)
                        : CreateBacktrackHighlightBackground(row, col);
                }
                else
                {
                    box.Background = CreateDisplayCellBackground(row, col, wasGiven);
                }
            }
        }
        _isBoardUpdating = false;
        SetBoardEditable(false);
    }

    private void ClearBoard()
    {
        _isBoardUpdating = true;
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _inputBoxes[row, col].Text = string.Empty;
                _inputBoxes[row, col].Background = CreateInputCellBackground(row, col);
                _inputBoxes[row, col].Foreground = new SolidColorBrush(Color.FromRgb(41, 57, 52));
                _inputBoxes[row, col].FontWeight = FontWeights.SemiBold;
            }
        }
        _isBoardUpdating = false;
        SetBoardEditable(true);
    }

    /// <summary>
    /// 求解入口：先校验，再求解并根据步骤列表启动演示会话。
    /// </summary>
    private void SolveButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("用户点击求解");
        var board = ReadInputBoard();
        _lastInput = SudokuSolver.CopyBoard(board);

        if (!SudokuValidator.ValidatePartial(board, out var partialError))
        {
            AppLogger.Info($"输入校验失败: {partialError}");
            RenderBoardState(board, board, null);
            SetBoardEditable(true);
            StatusText.Text = $"输入校验失败：{partialError}";
            return;
        }

        // 求解前先复制棋盘，避免直接覆盖用户输入区内容。
        var workingBoard = SudokuSolver.CopyBoard(board);
        var solved = SudokuSolver.SolveWithSteps(workingBoard, out var steps);
        if (!solved)
        {
            AppLogger.Info("求解失败: 无解");
            RenderBoardState(board, board, null);
            SetBoardEditable(true);
            StatusText.Text = "该题无解，请检查输入题目。";
            return;
        }

        if (!SudokuValidator.ValidateComplete(workingBoard, out var completeError))
        {
            AppLogger.Info($"结果校验失败: {completeError}");
            RenderBoardState(board, board, null);
            SetBoardEditable(true);
            StatusText.Text = $"结果校验失败：{completeError}";
            return;
        }

        if (steps.Count == 0)
        {
            FillSolvedBoard(workingBoard, _lastInput);
            _playbackSolvedBoard = SudokuSolver.CopyBoard(workingBoard);
            _playbackStartBoard = SudokuSolver.CopyBoard(board);
            _playbackSteps = new List<SudokuSolveStep>();
            _currentStepIndex = 0;
            UpdatePuzzleStats(board, 0);
            UpdatePlaybackInfoDisplay();
            StatusText.Text = "该题已经是完整有效解，无需演示回溯过程。";
            AppLogger.Info("输入题盘已是完整有效解");
            return;
        }

        var hasMultipleSolutions = SudokuSolver.HasMultipleSolutions(board);
        StartPlaybackSession(board, workingBoard, steps, hasMultipleSolutions);
        AppLogger.Info($"求解成功，开始过程演示，多解标记: {hasMultipleSolutions}");
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackLoaded || _playbackCompleted)
        {
            return;
        }

        if (_solveTimer.IsEnabled)
        {
            _solveTimer.Stop();
            _playbackPaused = true;
            StatusText.Text = $"已暂停：当前第 {_currentStepIndex} / {_playbackSteps.Count} 步，可手动上一步或下一步。";
        }
        else
        {
            _playbackPaused = false;
            _solveTimer.Start();
            StatusText.Text = $"继续播放：从第 {_currentStepIndex + 1} 步开始。";
        }

        UpdateControlStates();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackLoaded)
        {
            return;
        }

        _solveTimer.Stop();
        _playbackPaused = false;
        _playbackCompleted = false;
        _currentStepIndex = 0;
        _playbackDisplayBoard = SudokuSolver.CopyBoard(_playbackStartBoard);
        RenderBoardState(_playbackDisplayBoard, _playbackStartBoard, null);
        StatusText.Text = "已停止演示，结果盘已回到起点。可重新开始或清空后重新输入。";
        UpdateControlStates();
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackLoaded)
        {
            return;
        }

        _solveTimer.Stop();
        _playbackPaused = false;
        _playbackCompleted = false;
        _currentStepIndex = 0;
        _playbackDisplayBoard = SudokuSolver.CopyBoard(_playbackStartBoard);
        RenderBoardState(_playbackDisplayBoard, _playbackStartBoard, null);
        _solveTimer.Start();
        StatusText.Text = "已重新开始演示。";
        UpdateControlStates();
    }

    private void PreviousStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackPaused)
        {
            return;
        }

        if (!RewindPlaybackStep())
        {
            StatusText.Text = "已经在起点，无法继续回退。";
        }
    }

    private void NextStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackPaused)
        {
            return;
        }

        if (!AdvancePlaybackStep(isManual: true))
        {
            StatusText.Text = "已经到达最后一步。";
        }
    }

    private void CompleteNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playbackLoaded)
        {
            return;
        }

        _currentStepIndex = _playbackSteps.Count;
        FinalizePlayback(showDirectCompleteStatus: true);
    }

    private void GenerateEasyButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratePuzzle(SudokuDifficulty.Easy, "容易");
    }

    private void GenerateMediumButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratePuzzle(SudokuDifficulty.Medium, "中等");
    }

    private void GenerateHardButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratePuzzle(SudokuDifficulty.Hard, "困难");
    }

    /// <summary>
    /// 按难度生成新题，并预估演示步数用于信息面板展示。
    /// </summary>
    private void GeneratePuzzle(SudokuDifficulty difficulty, string difficultyText)
    {
        ResetPlaybackSession(clearOutput: true);
        var puzzle = SudokuGenerator.Generate(difficulty);
        var previewBoard = SudokuSolver.CopyBoard(puzzle);
        _ = SudokuSolver.SolveWithSteps(previewBoard, out var previewSteps);
        var clueCount = SudokuGenerator.CountClues(puzzle);
        var blankCount = 81 - clueCount;

        _currentPuzzleDifficultyText = difficultyText;
        _lastInput = SudokuSolver.CopyBoard(puzzle);
        FillInputBoard(puzzle);
        PuzzleStatsText.Text = $"题目统计：{difficultyText}，线索 {clueCount}，空格 {blankCount}，预计演示 {previewSteps.Count} 步。";
        PlaybackSummaryText.Text = "过程统计：等待开始。";
        PlaybackProgressText.Text = "播放进度：0 / 0";
        PlaybackProgressBar.Maximum = Math.Max(1, previewSteps.Count);
        PlaybackProgressBar.Value = 0;
        StatusText.Text = $"已生成{difficultyText}题目，可直接求解或手动修改。";
        AppLogger.Info($"生成新题：{difficultyText}");
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPlaybackSession(clearOutput: false);

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _inputBoxes[row, col].Text = string.Empty;
            }
        }

        _lastInput = new int[9, 9];
        _currentPuzzleDifficultyText = "手动输入";
        ClearBoard();
        UpdateInputConflictHighlight();
        PuzzleStatsText.Text = "题目统计：尚未生成或求解。";
        PlaybackSummaryText.Text = "过程统计：等待开始。";
        PlaybackProgressText.Text = "播放进度：0 / 0";
        PlaybackProgressBar.Maximum = 1;
        PlaybackProgressBar.Value = 0;
        AppLogger.Info("用户清空输入与输出");
        StatusText.Text = "已清空，可重新输入题目。";
    }

    private void SolveTimerOnTick(object? sender, EventArgs e)
    {
        _ = AdvancePlaybackStep(isManual: false);
    }

    /// <summary>
    /// 初始化一次演示会话并启动自动播放。
    /// </summary>
    private void StartPlaybackSession(int[,] originalBoard, int[,] solvedBoard, List<SudokuSolveStep> steps, bool hasMultipleSolutions)
    {
        ResetPlaybackSession(clearOutput: false);
        _playbackLoaded = true;
        _playbackPaused = false;
        _playbackCompleted = false;
        _playbackHasMultipleSolutions = hasMultipleSolutions;
        _playbackStartBoard = SudokuSolver.CopyBoard(originalBoard);
        _playbackSolvedBoard = SudokuSolver.CopyBoard(solvedBoard);
        _playbackDisplayBoard = SudokuSolver.CopyBoard(originalBoard);
        _playbackSteps = new List<SudokuSolveStep>(steps);
        _currentStepIndex = 0;

        UpdatePuzzleStats(originalBoard, steps.Count);
        RenderBoardState(_playbackDisplayBoard, _playbackStartBoard, null);
        _solveTimer.Start();
        StatusText.Text = $"开始演示：共 {_playbackSteps.Count} 步，播放速度为每秒 2 步。";
        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
    }

    /// <summary>
    /// 前进一步（自动或手动），并同步刷新棋盘、状态和进度。
    /// </summary>
    private bool AdvancePlaybackStep(bool isManual)
    {
        if (!_playbackLoaded || _currentStepIndex >= _playbackSteps.Count)
        {
            return false;
        }

        var step = _playbackSteps[_currentStepIndex];
        ApplySolveStep(_playbackDisplayBoard, step);
        _currentStepIndex++;
        RenderBoardState(_playbackDisplayBoard, _playbackStartBoard, step);

        if (_currentStepIndex >= _playbackSteps.Count)
        {
            FinalizePlayback(showDirectCompleteStatus: false);
            return true;
        }

        var actionText = step.StepType == SudokuSolveStepType.Fill
            ? $"第 {step.Row + 1} 行第 {step.Col + 1} 列填入 {step.Value}"
            : $"撤销第 {step.Row + 1} 行第 {step.Col + 1} 列的 {step.Value}";

        StatusText.Text = isManual
            ? $"最近动作：{actionText}（手动）"
            : $"最近动作：{actionText}";

        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
        return true;
    }

    /// <summary>
    /// 回退一步，仅允许在暂停状态下触发。
    /// </summary>
    private bool RewindPlaybackStep()
    {
        if (!_playbackLoaded || _currentStepIndex <= 0)
        {
            return false;
        }

        _solveTimer.Stop();
        _playbackPaused = true;
        _playbackCompleted = false;
        _currentStepIndex--;

        var step = _playbackSteps[_currentStepIndex];
        ReverseSolveStep(_playbackDisplayBoard, step);
        RenderBoardState(_playbackDisplayBoard, _playbackStartBoard, step);
        StatusText.Text = step.StepType == SudokuSolveStepType.Fill
            ? $"最近动作：回退第 {step.Row + 1} 行第 {step.Col + 1} 列，已撤销填入"
            : $"最近动作：回退第 {step.Row + 1} 行第 {step.Col + 1} 列，已撤销回溯";
        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
        return true;
    }

    /// <summary>
    /// 结束演示并展示最终解。
    /// </summary>
    private void FinalizePlayback(bool showDirectCompleteStatus)
    {
        _solveTimer.Stop();
        _playbackPaused = false;
        _playbackCompleted = true;
        FillSolvedBoard(_playbackSolvedBoard, _playbackStartBoard);
        StatusText.Text = showDirectCompleteStatus
            ? "已直接完成：跳过剩余演示并展示最终有效解。"
            : _playbackHasMultipleSolutions
                ? "求解完成：已展示完整过程。存在多解，当前展示其中一个有效解。"
                : "求解完成：已展示完整过程，并通过数独规则校验。";
        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
    }

    /// <summary>
    /// 重置播放状态机。clearOutput 为 true 时同时清空棋盘显示。
    /// </summary>
    private void ResetPlaybackSession(bool clearOutput)
    {
        _solveTimer.Stop();
        _playbackLoaded = false;
        _playbackPaused = false;
        _playbackCompleted = false;
        _playbackHasMultipleSolutions = false;
        _currentStepIndex = 0;
        _playbackStartBoard = new int[9, 9];
        _playbackDisplayBoard = new int[9, 9];
        _playbackSolvedBoard = new int[9, 9];
        _playbackSteps = new List<SudokuSolveStep>();

        if (clearOutput)
        {
            ClearBoard();
        }

        UpdateControlStates();
        UpdatePlaybackInfoDisplay();
    }

    private static void ApplySolveStep(int[,] board, SudokuSolveStep step)
    {
        board[step.Row, step.Col] = step.StepType == SudokuSolveStepType.Fill ? step.Value : 0;
    }

    private static void ReverseSolveStep(int[,] board, SudokuSolveStep step)
    {
        board[step.Row, step.Col] = step.StepType == SudokuSolveStepType.Fill ? 0 : step.Value;
    }

    /// <summary>
    /// 根据当前播放状态统一管理按钮可用性和模式标记。
    /// </summary>
    private void UpdateControlStates()
    {
        var isPlaying = _solveTimer.IsEnabled;
        var canManualStep = _playbackLoaded && _playbackPaused && !_playbackCompleted;

        SolveButton.IsEnabled = !_playbackLoaded;
        PauseResumeButton.IsEnabled = _playbackLoaded && !_playbackCompleted && _playbackSteps.Count > 0;
        PauseResumeButton.Content = _playbackPaused ? "继续" : "暂停";
        StopButton.IsEnabled = _playbackLoaded && (_currentStepIndex > 0 || isPlaying || _playbackPaused || _playbackCompleted);
        RestartButton.IsEnabled = _playbackLoaded && _playbackSteps.Count > 0;
        PreviousStepButton.IsEnabled = canManualStep && _currentStepIndex > 0;
        NextStepButton.IsEnabled = canManualStep && _currentStepIndex < _playbackSteps.Count;
        CompleteNowButton.IsEnabled = _playbackLoaded && !_playbackCompleted;
        ClearButton.IsEnabled = !isPlaying;
        GenerateEasyButton.IsEnabled = !isPlaying;
        GenerateMediumButton.IsEnabled = !isPlaying;
        GenerateHardButton.IsEnabled = !isPlaying;

        UpdateBoardModeIndicator();
    }

    private void SetBoardEditable(bool editable)
    {
        _isBoardEditable = editable;

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var box = _inputBoxes[row, col];
                box.IsReadOnly = !editable;
                box.Cursor = editable ? Cursors.IBeam : Cursors.Arrow;
                box.Focusable = editable;
            }
        }
    }

    /// <summary>
    /// 更新题目统计文本（难度、线索、空格、预计步数）。
    /// </summary>
    private void UpdatePuzzleStats(int[,] board, int estimatedStepCount)
    {
        var clueCount = SudokuGenerator.CountClues(board);
        var blankCount = 81 - clueCount;
        PuzzleStatsText.Text = $"题目统计：{_currentPuzzleDifficultyText}，线索 {clueCount}，空格 {blankCount}，预计演示 {estimatedStepCount} 步。";
    }

    /// <summary>
    /// 更新过程统计与进度条显示。
    /// </summary>
    private void UpdatePlaybackInfoDisplay()
    {
        var totalSteps = _playbackSteps.Count;
        PlaybackProgressBar.Maximum = Math.Max(1, totalSteps);
        PlaybackProgressBar.Value = Math.Min(_currentStepIndex, Math.Max(1, totalSteps));
        PlaybackProgressText.Text = $"播放进度：{_currentStepIndex} / {totalSteps}";

        PlaybackSummaryText.Text = !_playbackLoaded
            ? "过程统计：等待开始。"
            : _playbackCompleted
                ? $"过程统计：已完成，共 {totalSteps} 步。"
                : _playbackPaused
                    ? $"过程统计：已暂停，当前第 {_currentStepIndex} / {totalSteps} 步。"
                    : $"过程统计：自动播放中，当前第 {_currentStepIndex} / {totalSteps} 步。";
    }

    /// <summary>
    /// 刷新棋盘模式徽标与外框配色，帮助用户快速识别当前状态。
    /// </summary>
    private void UpdateBoardModeIndicator()
    {
        string text;
        Color background;
        Color foreground;
        Color borderColor;

        if (_playbackCompleted)
        {
            text = "完成模式";
            background = Color.FromRgb(232, 242, 226);
            foreground = Color.FromRgb(46, 90, 58);
            borderColor = Color.FromRgb(176, 212, 168);
        }
        else if (_playbackPaused)
        {
            text = "暂停模式";
            background = Color.FromRgb(247, 236, 210);
            foreground = Color.FromRgb(122, 86, 28);
            borderColor = Color.FromRgb(232, 201, 122);
        }
        else if (_solveTimer.IsEnabled)
        {
            text = "演示模式";
            background = Color.FromRgb(228, 239, 248);
            foreground = Color.FromRgb(43, 83, 126);
            borderColor = Color.FromRgb(176, 200, 224);
        }
        else if (_playbackLoaded)
        {
            text = "结果模式";
            background = Color.FromRgb(239, 233, 245);
            foreground = Color.FromRgb(88, 67, 118);
            borderColor = Color.FromRgb(208, 192, 224);
        }
        else
        {
            text = "编辑模式";
            background = Color.FromRgb(232, 239, 227);
            foreground = Color.FromRgb(46, 90, 58);
            borderColor = Color.FromRgb(232, 228, 218);
        }

        BoardModeText.Text = text;
        BoardModeText.Foreground = new SolidColorBrush(foreground);
        BoardModeBadge.Background = new SolidColorBrush(background);
        BoardBorderPanel.BorderBrush = new SolidColorBrush(borderColor);
    }

}