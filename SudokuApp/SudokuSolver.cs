namespace SudokuApp;

/// <summary>
/// 负责数独求解与多解判定。
/// 当前实现采用回溯搜索，并优先选择候选数最少的空格以减少搜索分支。
/// </summary>
public static class SudokuSolver
{
    public static int[,] CopyBoard(int[,] board)
    {
        var copy = new int[9, 9];
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                copy[row, col] = board[row, col];
            }
        }

        return copy;
    }

    public static bool Solve(int[,] board)
    {
        return SolveInternal(board);
    }

    public static bool HasMultipleSolutions(int[,] board)
    {
        var copy = CopyBoard(board);
        var count = CountSolutions(copy, 2);
        return count > 1;
    }

    private static bool SolveInternal(int[,] board)
    {
        if (!FindBestEmptyCell(board, out var row, out var col, out var candidates))
        {
            return true;
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            board[row, col] = candidate;
            if (SolveInternal(board))
            {
                return true;
            }
            board[row, col] = 0;
        }

        return false;
    }

    private static int CountSolutions(int[,] board, int limit)
    {
        if (!FindBestEmptyCell(board, out var row, out var col, out var candidates))
        {
            return 1;
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var total = 0;
        foreach (var candidate in candidates)
        {
            board[row, col] = candidate;
            total += CountSolutions(board, limit);
            board[row, col] = 0;

            if (total >= limit)
            {
                return total;
            }
        }

        return total;
    }

    private static bool FindBestEmptyCell(int[,] board, out int bestRow, out int bestCol, out List<int> bestCandidates)
    {
        bestRow = -1;
        bestCol = -1;
        bestCandidates = new List<int>();
        var minCount = int.MaxValue;

        // 选择候选数最少的空格，可以显著降低回溯树的宽度。
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                if (board[row, col] != 0)
                {
                    continue;
                }

                var candidates = GetCandidates(board, row, col);
                if (candidates.Count < minCount)
                {
                    minCount = candidates.Count;
                    bestRow = row;
                    bestCol = col;
                    bestCandidates = candidates;
                    if (minCount == 0)
                    {
                        return true;
                    }
                }
            }
        }

        return bestRow != -1;
    }

    private static List<int> GetCandidates(int[,] board, int row, int col)
    {
        var used = new bool[10];

        // 同时扫描行、列和 3x3 宫，汇总当前位置不能使用的数字。
        for (var i = 0; i < 9; i++)
        {
            if (board[row, i] != 0)
            {
                used[board[row, i]] = true;
            }

            if (board[i, col] != 0)
            {
                used[board[i, col]] = true;
            }
        }

        var boxRowStart = row / 3 * 3;
        var boxColStart = col / 3 * 3;
        for (var r = boxRowStart; r < boxRowStart + 3; r++)
        {
            for (var c = boxColStart; c < boxColStart + 3; c++)
            {
                if (board[r, c] != 0)
                {
                    used[board[r, c]] = true;
                }
            }
        }

        var candidates = new List<int>(9);
        for (var value = 1; value <= 9; value++)
        {
            if (!used[value])
            {
                candidates.Add(value);
            }
        }

        return candidates;
    }
}
