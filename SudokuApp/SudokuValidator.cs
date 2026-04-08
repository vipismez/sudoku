namespace SudokuApp;

/// <summary>
/// 负责基于标准数独规则验证题盘与结果盘。
/// </summary>
public static class SudokuValidator
{
    public static bool ValidatePartial(int[,] board, out string error)
    {
        error = string.Empty;

        // 题目阶段允许空格，但已填写数字必须满足行、列、宫内不重复。
        for (var row = 0; row < 9; row++)
        {
            var seen = new bool[10];
            for (var col = 0; col < 9; col++)
            {
                var value = board[row, col];
                if (value == 0)
                {
                    continue;
                }

                if (value is < 1 or > 9)
                {
                    error = $"第 {row + 1} 行第 {col + 1} 列的数字不在 1-9 范围内。";
                    return false;
                }

                if (seen[value])
                {
                    error = $"第 {row + 1} 行存在重复数字 {value}。";
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var col = 0; col < 9; col++)
        {
            var seen = new bool[10];
            for (var row = 0; row < 9; row++)
            {
                var value = board[row, col];
                if (value == 0)
                {
                    continue;
                }

                if (seen[value])
                {
                    error = $"第 {col + 1} 列存在重复数字 {value}。";
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var boxRow = 0; boxRow < 3; boxRow++)
        {
            for (var boxCol = 0; boxCol < 3; boxCol++)
            {
                var seen = new bool[10];
                for (var row = boxRow * 3; row < boxRow * 3 + 3; row++)
                {
                    for (var col = boxCol * 3; col < boxCol * 3 + 3; col++)
                    {
                        var value = board[row, col];
                        if (value == 0)
                        {
                            continue;
                        }

                        if (seen[value])
                        {
                            error = $"第 {boxRow + 1} 行宫、第 {boxCol + 1} 列宫存在重复数字 {value}。";
                            return false;
                        }

                        seen[value] = true;
                    }
                }
            }
        }

        return true;
    }

    public static bool ValidateComplete(int[,] board, out string error)
    {
        error = string.Empty;

        // 结果阶段要求每个位置都是 1-9 的有效数字，且不能重复。
        for (var row = 0; row < 9; row++)
        {
            var seen = new bool[10];
            for (var col = 0; col < 9; col++)
            {
                var value = board[row, col];
                if (value is < 1 or > 9)
                {
                    error = $"结果中第 {row + 1} 行第 {col + 1} 列不是有效数字。";
                    return false;
                }

                if (seen[value])
                {
                    error = $"结果中第 {row + 1} 行出现重复数字 {value}。";
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var col = 0; col < 9; col++)
        {
            var seen = new bool[10];
            for (var row = 0; row < 9; row++)
            {
                var value = board[row, col];
                if (seen[value])
                {
                    error = $"结果中第 {col + 1} 列出现重复数字 {value}。";
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var boxRow = 0; boxRow < 3; boxRow++)
        {
            for (var boxCol = 0; boxCol < 3; boxCol++)
            {
                var seen = new bool[10];
                for (var row = boxRow * 3; row < boxRow * 3 + 3; row++)
                {
                    for (var col = boxCol * 3; col < boxCol * 3 + 3; col++)
                    {
                        var value = board[row, col];
                        if (seen[value])
                        {
                            error = $"结果中第 {boxRow + 1} 行宫、第 {boxCol + 1} 列宫有重复数字 {value}。";
                            return false;
                        }

                        seen[value] = true;
                    }
                }
            }
        }

        return true;
    }
}
