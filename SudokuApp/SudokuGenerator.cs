namespace SudokuApp;

public enum SudokuDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// 负责生成带唯一解的 9x9 数独题目。
/// </summary>
public static class SudokuGenerator
{
    private static readonly Random Random = new();

    /// <summary>
    /// 统计当前题盘的已知数字数量（线索数）。
    /// </summary>
    public static int CountClues(int[,] board)
    {
        var count = 0;
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                if (board[row, col] != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 按难度生成题目。
    /// 难度通过目标线索数控制，并在挖空过程中维持唯一解。
    /// </summary>
    public static int[,] Generate(SudokuDifficulty difficulty)
    {
        var targetClues = difficulty switch
        {
            SudokuDifficulty.Easy => 40,
            SudokuDifficulty.Medium => 32,
            SudokuDifficulty.Hard => 26,
            _ => 32
        };

        int[,]? bestPuzzle = null;
        var bestClueCount = int.MaxValue;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var solvedBoard = CreateSolvedBoard();
            var puzzle = SudokuSolver.CopyBoard(solvedBoard);
            RemoveCellsWhileKeepingUniqueSolution(puzzle, targetClues);

            var clueCount = CountClues(puzzle);
            if (clueCount < bestClueCount)
            {
                bestClueCount = clueCount;
                bestPuzzle = SudokuSolver.CopyBoard(puzzle);
            }

            if (clueCount <= targetClues)
            {
                break;
            }
        }

        return bestPuzzle ?? CreateSolvedBoard();
    }

    /// <summary>
    /// 先构造一个完整终盘，再由挖空流程生成题盘。
    /// </summary>
    private static int[,] CreateSolvedBoard()
    {
        var rowOrder = BuildShuffledAxisOrder();
        var colOrder = BuildShuffledAxisOrder();
        var digitOrder = Shuffle(Enumerable.Range(1, 9).ToList());
        var board = new int[9, 9];

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var patternIndex = (rowOrder[row] * 3 + rowOrder[row] / 3 + colOrder[col]) % 9;
                board[row, col] = digitOrder[patternIndex];
            }
        }

        return board;
    }

    /// <summary>
    /// 生成被打乱的行/列顺序（先打乱宫组，再打乱组内索引）。
    /// </summary>
    private static List<int> BuildShuffledAxisOrder()
    {
        var groups = Shuffle(new List<int> { 0, 1, 2 });
        var order = new List<int>(9);

        foreach (var group in groups)
        {
            var inner = Shuffle(new List<int> { 0, 1, 2 });
            foreach (var index in inner)
            {
                order.Add(group * 3 + index);
            }
        }

        return order;
    }

    /// <summary>
    /// 随机尝试挖空；若挖空后出现多解，则回退本次挖空。
    /// </summary>
    private static void RemoveCellsWhileKeepingUniqueSolution(int[,] puzzle, int targetClues)
    {
        var positions = Shuffle(Enumerable.Range(0, 81).ToList());

        foreach (var position in positions)
        {
            if (CountClues(puzzle) <= targetClues)
            {
                return;
            }

            var row = position / 9;
            var col = position % 9;
            var backup = puzzle[row, col];
            puzzle[row, col] = 0;

            if (SudokuSolver.HasMultipleSolutions(puzzle))
            {
                puzzle[row, col] = backup;
            }
        }
    }

    /// <summary>
    /// Fisher-Yates 洗牌。
    /// </summary>
    private static List<T> Shuffle<T>(List<T> source)
    {
        for (var index = source.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Next(index + 1);
            (source[index], source[swapIndex]) = (source[swapIndex], source[index]);
        }

        return source;
    }
}