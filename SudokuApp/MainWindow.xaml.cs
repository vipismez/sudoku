using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SudokuApp;

public partial class MainWindow : Window
{
    private readonly TextBox[,] _inputBoxes = new TextBox[9, 9];
    private readonly Border[,] _outputCells = new Border[9, 9];
    private readonly TextBlock[,] _outputTexts = new TextBlock[9, 9];

    private readonly SudokuImageRecognizer _imageRecognizer;
    private int[,] _lastInput = new int[9, 9];

    public MainWindow()
    {
        InitializeComponent();
        _imageRecognizer = new SudokuImageRecognizer();
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
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(col % 3 == 2 ? 3 : 1, row % 3 == 2 ? 3 : 1, 1, 1),
                    MaxLength = 1,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(184, 189, 178)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(251, 252, 248))
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
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(189, 197, 184)),
                    Margin = new Thickness(col % 3 == 2 ? 3 : 1, row % 3 == 2 ? 3 : 1, 1, 1),
                    Background = Brushes.White,
                    Child = text
                };

                _outputCells[row, col] = cell;
                _outputTexts[row, col] = text;
                OutputGrid.Children.Add(cell);
            }
        }
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
            return;
        }

        if (!IsDigitOneToNine(box.Text))
        {
            box.Text = string.Empty;
            return;
        }

        box.CaretIndex = box.Text.Length;
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
                _outputCells[row, col].Background = wasGiven
                    ? new SolidColorBrush(Color.FromRgb(243, 246, 238))
                    : new SolidColorBrush(Color.FromRgb(252, 246, 235));
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
                _outputCells[row, col].Background = Brushes.White;
            }
        }
    }

    private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Title = "选择数独题目图片"
        };

        if (picker.ShowDialog() != true)
        {
            AppLogger.Info("用户取消了图片选择");
            return;
        }

        AppLogger.Info($"开始识别图片: {picker.FileName}");

        try
        {
            ToggleButtons(false);
            StatusText.Text = "正在识别图片，请稍候...";
            var board = await _imageRecognizer.RecognizeFromImageAsync(picker.FileName);
            FillInputBoard(board);
            ClearOutputBoard();
            AppLogger.Info("图片识别完成并填入输入表格");
            StatusText.Text = "图片识别完成。请检查题目后点击“求解”。";
        }
        catch (Exception ex)
        {
            AppLogger.Error("图片识别流程失败", ex);
            StatusText.Text = $"图片识别失败：{ex.Message}";
        }
        finally
        {
            ToggleButtons(true);
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
        AppLogger.Info("用户清空输入与输出");
        StatusText.Text = "已清空，可重新输入题目。";
    }

    private void ToggleButtons(bool enabled)
    {
        UploadImageButton.IsEnabled = enabled;
        SolveButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
    }
}