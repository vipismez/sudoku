using System.IO;
using System.Net.Http;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;
using Tesseract;

namespace SudokuApp;

public class SudokuImageRecognizer
{
    private const int GridSize = 450;

    public async Task<int[,]> RecognizeFromImageAsync(string imagePath)
    {
        var tessdataDir = await EnsureTessdataAsync();

        using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (src.Empty())
        {
            throw new InvalidOperationException("无法读取图片文件。");
        }

        using var warped = ExtractSudokuBoard(src);
        if (warped.Empty())
        {
            throw new InvalidOperationException("未检测到有效数独网格，请更换更清晰的图片。");
        }

        return RecognizeCells(warped, tessdataDir);
    }

    private static Mat ExtractSudokuBoard(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        using var blur = new Mat();
        Cv2.GaussianBlur(gray, blur, new Size(9, 9), 0);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(blur, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 11, 2);

        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return new Mat();
        }

        Point[]? bestQuad = null;
        var maxArea = 0.0;
        foreach (var contour in contours)
        {
            var perimeter = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true);
            if (approx.Length != 4)
            {
                continue;
            }

            var area = Cv2.ContourArea(approx);
            if (area > maxArea)
            {
                maxArea = area;
                bestQuad = approx;
            }
        }

        if (bestQuad is null)
        {
            return new Mat();
        }

        var ordered = OrderPoints(bestQuad);
        var dstPoints = new[]
        {
            new Point2f(0, 0),
            new Point2f(GridSize - 1, 0),
            new Point2f(GridSize - 1, GridSize - 1),
            new Point2f(0, GridSize - 1)
        };

        using var matrix = Cv2.GetPerspectiveTransform(ordered, dstPoints);
        var warped = new Mat();
        Cv2.WarpPerspective(gray, warped, matrix, new Size(GridSize, GridSize));

        using var finalBinary = new Mat();
        Cv2.AdaptiveThreshold(warped, finalBinary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);
        return finalBinary.Clone();
    }

    private static int[,] RecognizeCells(Mat warpedBoard, string tessdataDir)
    {
        var board = new int[9, 9];
        var cellSize = GridSize / 9;

        using var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", "123456789");
        engine.DefaultPageSegMode = PageSegMode.SingleChar;

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var rect = new Rect(col * cellSize, row * cellSize, cellSize, cellSize);
                using var cell = new Mat(warpedBoard, rect);
                using var roi = CropInner(cell, 0.2);

                var nonWhite = roi.Rows * roi.Cols - Cv2.CountNonZero(roi);
                if (nonWhite < 35)
                {
                    board[row, col] = 0;
                    continue;
                }

                using var inv = new Mat();
                Cv2.Threshold(roi, inv, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                using var resized = new Mat();
                Cv2.Resize(inv, resized, new Size(80, 80), 0, 0, InterpolationFlags.Cubic);
                var pngBytes = resized.ImEncode(".png");
                using var pix = Pix.LoadFromMemory(pngBytes);
                using var page = engine.Process(pix);
                var text = page.GetText().Trim();

                board[row, col] = text.Length > 0 && char.IsDigit(text[0]) && text[0] is >= '1' and <= '9'
                    ? text[0] - '0'
                    : 0;
            }
        }

        return board;
    }

    private static Mat CropInner(Mat cell, double ratio)
    {
        var marginX = (int)(cell.Cols * ratio);
        var marginY = (int)(cell.Rows * ratio);
        var width = Math.Max(1, cell.Cols - marginX * 2);
        var height = Math.Max(1, cell.Rows - marginY * 2);
        var rect = new Rect(marginX, marginY, width, height);
        return new Mat(cell, rect).Clone();
    }

    private static Point2f[] OrderPoints(Point[] points)
    {
        var pts = points.Select(p => new Point2f(p.X, p.Y)).ToArray();
        var ordered = new Point2f[4];

        ordered[0] = pts.OrderBy(p => p.X + p.Y).First();
        ordered[2] = pts.OrderByDescending(p => p.X + p.Y).First();
        ordered[1] = pts.OrderBy(p => p.X - p.Y).First();
        ordered[3] = pts.OrderByDescending(p => p.X - p.Y).First();

        return ordered;
    }

    private static async Task<string> EnsureTessdataAsync()
    {
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SudokuApp");
        var tessdataDir = Path.Combine(appDataDir, "tessdata");
        Directory.CreateDirectory(tessdataDir);

        var trainedDataPath = Path.Combine(tessdataDir, "eng.traineddata");
        if (!File.Exists(trainedDataPath))
        {
            using var client = new HttpClient();
            var url = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata";
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(trainedDataPath, bytes);
        }

        return appDataDir;
    }
}
