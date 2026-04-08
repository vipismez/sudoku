using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SudokuApp;

/// <summary>
/// 主界面负责手动输入数独、冲突提示、求解与答案展示。
/// </summary>
public partial class MainWindow : Window
{
    private readonly record struct CellPosition(int Row, int Col);

    private readonly TextBox[,] _inputBoxes = new TextBox[9, 9];
    private readonly Border[,] _outputCells = new Border[9, 9];
    private readonly TextBlock[,] _outputTexts = new TextBlock[9, 9];

    private int[,] _lastInput = new int[9, 9];

    public MainWindow()
    {
        InitializeComponent();
        BuildInputGrid();
        BuildOutputGrid();
        AppLogger.Info("主窗口初始化完成");
    }

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

    private void BuildOutputGrid()
    {
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var text = new TextBlock
                {
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(40, 58, 52))
                };

                var cell = new Border
                {
                    BorderThickness = GetCellBorderThickness(row, col),
                    BorderBrush = CreateCellBorderBrush(),
                    Margin = new Thickness(0),
                    Background = CreateOutputCellBackground(row, col, true),
                    Child = text
                };

                _outputCells[row, col] = cell;
                _outputTexts[row, col] = text;
                OutputGrid.Children.Add(cell);
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

    private static SolidColorBrush CreateOutputCellBackground(int row, int col, bool isOriginal)
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

    private static bool IsDigitOneToNine(string input)
    {
        return input.Length == 1 && input[0] is >= '1' and <= '9';
    }

    private void InputBoxOnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigitOneToNine(e.Text);
    }

    private void InputBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
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
                _inputBoxes[row, col].Background = conflicts[row, col]
                    ? CreateConflictBackground(row, col)
                    : CreateInputCellBackground(row, col);
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
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _inputBoxes[row, col].Text = board[row, col] == 0 ? string.Empty : board[row, col].ToString();
            }
        }

        UpdateInputConflictHighlight();
    }

    private void FillOutputBoard(int[,] solved, int[,] original)
    {
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var wasGiven = original[row, col] != 0;
                _outputTexts[row, col].Text = solved[row, col].ToString();
                _outputTexts[row, col].Foreground = wasGiven
                    ? new SolidColorBrush(Color.FromRgb(38, 58, 51))
                    : new SolidColorBrush(Color.FromRgb(120, 77, 33));
                _outputCells[row, col].Background = CreateOutputCellBackground(row, col, wasGiven);
            }
        }
    }

    private void ClearOutputBoard()
    {
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _outputTexts[row, col].Text = string.Empty;
                _outputCells[row, col].Background = CreateOutputCellBackground(row, col, true);
            }
        }
    }

    private void SolveButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("用户点击求解");
        var board = ReadInputBoard();
        _lastInput = SudokuSolver.CopyBoard(board);

        if (!SudokuValidator.ValidatePartial(board, out var partialError))
        {
            AppLogger.Info($"输入校验失败: {partialError}");
            ClearOutputBoard();
            StatusText.Text = $"输入校验失败：{partialError}";
            return;
        }

        // 求解前先复制棋盘，避免直接覆盖用户输入区内容。
        var workingBoard = SudokuSolver.CopyBoard(board);
        var solved = SudokuSolver.Solve(workingBoard);
        if (!solved)
        {
            AppLogger.Info("求解失败: 无解");
            ClearOutputBoard();
            StatusText.Text = "该题无解，请检查输入题目。";
            return;
        }

        if (!SudokuValidator.ValidateComplete(workingBoard, out var completeError))
        {
            AppLogger.Info($"结果校验失败: {completeError}");
            ClearOutputBoard();
            StatusText.Text = $"结果校验失败：{completeError}";
            return;
        }

        var hasMultipleSolutions = SudokuSolver.HasMultipleSolutions(board);
        FillOutputBoard(workingBoard, _lastInput);
        AppLogger.Info($"求解成功，多解标记: {hasMultipleSolutions}");
        StatusText.Text = hasMultipleSolutions
            ? "求解完成：存在多解，当前展示其中一个有效解。"
            : "求解完成：已通过数独规则校验。";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                _inputBoxes[row, col].Text = string.Empty;
            }
        }

        _lastInput = new int[9, 9];
        ClearOutputBoard();
        UpdateInputConflictHighlight();
        AppLogger.Info("用户清空输入与输出");
        StatusText.Text = "已清空，可重新输入题目。";
    }

}