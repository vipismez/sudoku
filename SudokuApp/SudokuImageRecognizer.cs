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
    private static readonly Dictionary<int, Mat> DigitTemplates = BuildDigitTemplates();

    public async Task<int[,]> RecognizeFromImageAsync(string imagePath)
    {
        AppLogger.Info($"OCR开始，图片路径: {imagePath}");
        var tessdataDir = await EnsureTessdataAsync();
        AppLogger.Info($"OCR模型目录: {tessdataDir}");

        using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (src.Empty())
        {
            throw new InvalidOperationException("无法读取图片文件。");
        }

        AppLogger.Info($"原图尺寸: {src.Width}x{src.Height}");

        using var warped = ExtractSudokuBoard(src);
        if (warped.Empty())
        {
            AppLogger.Error("棋盘定位失败，未提取到有效数独区域");
            throw new InvalidOperationException("未检测到有效数独网格，请更换更清晰的图片。");
        }

        var board = RecognizeCells(warped, tessdataDir);
        var clueCount = CountClues(board);
        AppLogger.Info($"OCR结束，识别到初始数字数量: {clueCount}");
        return board;
    }

    private static Mat ExtractSudokuBoard(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        using var blur = new Mat();
        Cv2.GaussianBlur(gray, blur, new Size(9, 9), 0);
        using var binaryInv = new Mat();
        Cv2.AdaptiveThreshold(blur, binaryInv, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 11, 2);

        using var edges = new Mat();
        Cv2.Canny(blur, edges, 80, 200);

        if (!TryFindSudokuQuad(binaryInv, gray.Size(), out var bestQuad) &&
            !TryFindSudokuQuad(edges, gray.Size(), out bestQuad))
        {
            if (!TryFallbackToWholeBoard(gray.Size(), out bestQuad))
            {
                return new Mat();
            }

            AppLogger.Info("启用近方图兜底：使用整图内缩区域作为棋盘候选");
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
        return warped.Clone();
    }

    private static bool TryFindSudokuQuad(Mat sourceMask, Size imageSize, out Point[] quad)
    {
        quad = Array.Empty<Point>();
        Cv2.FindContours(sourceMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return false;
        }

        var imageArea = imageSize.Width * imageSize.Height;
        var minArea = imageArea * 0.08;
        var maxArea = imageArea * 0.92;
        var imageCenter = new Point2f(imageSize.Width / 2f, imageSize.Height / 2f);

        var bestScore = double.NegativeInfinity;
        Point[]? best = null;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea || area > maxArea)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true);

            Point[] candidate;
            if (approx.Length == 4)
            {
                candidate = approx;
            }
            else
            {
                var rect = Cv2.MinAreaRect(contour);
                var pts = rect.Points();
                candidate = pts.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
            }

            if (candidate.Length != 4)
            {
                continue;
            }

            var rectBox = Cv2.BoundingRect(candidate);
            var ratio = (double)rectBox.Width / Math.Max(1, rectBox.Height);
            if (ratio < 0.72 || ratio > 1.38)
            {
                continue;
            }

            var center = new Point2f(rectBox.X + rectBox.Width / 2f, rectBox.Y + rectBox.Height / 2f);
            var centerDistance = Math.Sqrt(Math.Pow(center.X - imageCenter.X, 2) + Math.Pow(center.Y - imageCenter.Y, 2));
            var diagonal = Math.Sqrt(imageSize.Width * imageSize.Width + imageSize.Height * imageSize.Height);

            var normalizedArea = area / imageArea;
            var ratioPenalty = Math.Abs(1.0 - ratio);
            var centerPenalty = centerDistance / Math.Max(1.0, diagonal);
            var score = normalizedArea * 2.4 - ratioPenalty * 1.6 - centerPenalty * 0.7;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best is null)
        {
            AppLogger.Info("当前通道未找到合格四边形候选");
            return false;
        }

        quad = best;
        AppLogger.Info($"四边形候选定位成功，点数: {quad.Length}");
        return true;
    }

    private static bool TryFallbackToWholeBoard(Size size, out Point[] quad)
    {
        quad = Array.Empty<Point>();
        var ratio = (double)size.Width / Math.Max(1, size.Height);
        if (ratio < 0.78 || ratio > 1.28)
        {
            return false;
        }

        var insetX = Math.Max(4, (int)(size.Width * 0.015));
        var insetY = Math.Max(4, (int)(size.Height * 0.015));
        quad =
        [
            new Point(insetX, insetY),
            new Point(size.Width - 1 - insetX, insetY),
            new Point(size.Width - 1 - insetX, size.Height - 1 - insetY),
            new Point(insetX, size.Height - 1 - insetY)
        ];
        return true;
    }

    private static int[,] RecognizeCells(Mat warpedBoard, string tessdataDir)
    {
        var board = new int[9, 9];
        var cellSize = GridSize / 9;

        using var engine = CreateEngineWithRecovery(tessdataDir);
        if (engine is not null)
        {
            engine.SetVariable("tessedit_char_whitelist", "123456789");
            engine.DefaultPageSegMode = PageSegMode.SingleChar;
        }
        else
        {
            AppLogger.Info("Tesseract 不可用，使用模板匹配兜底识别");
        }

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var rect = new Rect(col * cellSize, row * cellSize, cellSize, cellSize);
                using var cell = new Mat(warpedBoard, rect);
                using var roi = CropInner(cell, 0.10);
                using var roiBlur = new Mat();
                Cv2.GaussianBlur(roi, roiBlur, new Size(3, 3), 0);
                using var inv = new Mat();
                Cv2.Threshold(roiBlur, inv, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                using var denoised = new Mat();
                Cv2.MedianBlur(inv, denoised, 3);

                var nonWhite = Cv2.CountNonZero(denoised);
                if (nonWhite < 10)
                {
                    board[row, col] = 0;
                    continue;
                }

                using var digitOnly = ExtractDigitRegion(denoised);
                using var resized = new Mat();
                Cv2.Resize(digitOnly, resized, new Size(96, 96), 0, 0, InterpolationFlags.Cubic);
                if (engine is not null)
                {
                    board[row, col] = TryReadDigitWithTesseract(engine, resized);
                }
                else
                {
                    board[row, col] = MatchDigitByTemplate(resized);
                }
            }
        }

        return board;
    }

    private static Mat ExtractDigitRegion(Mat invCell)
    {
        Cv2.FindContours(invCell, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return invCell.Clone();
        }

        Rect? bestRect = null;
        var bestArea = 0.0;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 10)
            {
                continue;
            }

            var rect = Cv2.BoundingRect(contour);
            var touchBorder = rect.X <= 1 || rect.Y <= 1 || rect.Right >= invCell.Cols - 1 || rect.Bottom >= invCell.Rows - 1;
            if (touchBorder)
            {
                continue;
            }

            if (area > bestArea)
            {
                bestArea = area;
                bestRect = rect;
            }
        }

        if (bestRect is null)
        {
            return invCell.Clone();
        }

        var r = bestRect.Value;
        var padX = Math.Max(1, (int)(r.Width * 0.18));
        var padY = Math.Max(1, (int)(r.Height * 0.18));

        var x = Math.Max(0, r.X - padX);
        var y = Math.Max(0, r.Y - padY);
        var w = Math.Min(invCell.Cols - x, r.Width + padX * 2);
        var h = Math.Min(invCell.Rows - y, r.Height + padY * 2);

        return new Mat(invCell, new Rect(x, y, w, h)).Clone();
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

    private static Task<string> EnsureTessdataAsync()
    {
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SudokuApp");
        var tessdataDir = Path.Combine(appDataDir, "tessdata");
        Directory.CreateDirectory(tessdataDir);

        var trainedDataPath = Path.Combine(tessdataDir, "eng.traineddata");
        if (!IsLikelyValidTrainedData(trainedDataPath))
        {
            AppLogger.Info("OCR 模型缺失或疑似损坏，将直接使用模板匹配兜底（不阻塞下载）");
        }

        return Task.FromResult(tessdataDir);
    }

    private static TesseractEngine? CreateEngineWithRecovery(string tessdataDir)
    {
        var candidatePaths = new[]
        {
            tessdataDir,
            Path.Combine(tessdataDir, "tessdata"),
            Directory.GetParent(tessdataDir)?.FullName ?? string.Empty
        }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Distinct()
        .ToArray();

        foreach (var path in candidatePaths)
        {
            try
            {
                AppLogger.Info($"尝试初始化 Tesseract，datapath: {path}");
                return new TesseractEngine(path, "eng", EngineMode.Default);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Tesseract 初始化失败，datapath: {path}", ex);
            }
        }

        AppLogger.Error("Tesseract 多路径初始化全部失败，切换模板匹配");
        return null;
    }

    private static int TryReadDigitWithTesseract(TesseractEngine engine, Mat resized)
    {
        try
        {
            var candidates = new List<Mat>();

            var inv = new Mat();
            Cv2.BitwiseNot(resized, inv);
            candidates.Add(inv);

            candidates.Add(resized.Clone());

            var thick = new Mat();
            using (var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2)))
            {
                Cv2.Dilate(inv, thick, kernel, iterations: 1);
            }
            candidates.Add(thick);

            try
            {
                foreach (var candidate in candidates)
                {
                    var bytes = candidate.ImEncode(".png");
                    using var pix = Pix.LoadFromMemory(bytes);
                    using var page = engine.Process(pix);
                    var text = page.GetText().Trim();
                    if (text.Length > 0 && char.IsDigit(text[0]) && text[0] is >= '1' and <= '9')
                    {
                        return text[0] - '0';
                    }
                }

                return 0;
            }
            finally
            {
                foreach (var m in candidates)
                {
                    m.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("单格 Tesseract 识别异常，返回空白", ex);
            return 0;
        }
    }

    private static int MatchDigitByTemplate(Mat resized)
    {
        using var normalized = new Mat();
        Cv2.Threshold(resized, normalized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        var bestDigit = 0;
        var bestScore = double.NegativeInfinity;

        foreach (var item in DigitTemplates)
        {
            using var result = new Mat();
            Cv2.MatchTemplate(normalized, item.Value, result, TemplateMatchModes.CCoeffNormed);
            result.GetArray(out float[] scores);
            var score = scores.Length > 0 ? scores[0] : -1f;
            if (score > bestScore)
            {
                bestScore = score;
                bestDigit = item.Key;
            }
        }

        return bestScore >= 0.12 ? bestDigit : 0;
    }

    private static Dictionary<int, Mat> BuildDigitTemplates()
    {
        var templates = new Dictionary<int, Mat>();
        for (var digit = 1; digit <= 9; digit++)
        {
            var mat = new Mat(new Size(96, 96), MatType.CV_8UC1, Scalar.All(255));
            Cv2.PutText(mat, digit.ToString(), new Point(16, 76), HersheyFonts.HersheySimplex, 2.45, Scalar.All(0), 4, LineTypes.AntiAlias);
            Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            templates[digit] = mat;
        }

        return templates;
    }

    private static bool IsLikelyValidTrainedData(string trainedDataPath)
    {
        try
        {
            if (!File.Exists(trainedDataPath))
            {
                return false;
            }

            var fileInfo = new FileInfo(trainedDataPath);
            if (fileInfo.Length < 1024 * 512)
            {
                return false;
            }

            using var fs = File.OpenRead(trainedDataPath);
            var header = new byte[Math.Min(16, (int)fs.Length)];
            _ = fs.Read(header, 0, header.Length);
            var headText = System.Text.Encoding.ASCII.GetString(header).ToLowerInvariant();
            return !headText.Contains("<html") && !headText.Contains("<!doctype") && !headText.Contains("<?xml");
        }
        catch
        {
            return false;
        }
    }

    private static async Task DownloadTrainedDataAsync(string trainedDataPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(trainedDataPath)!);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        var urls = new[]
        {
            "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata",
            "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata"
        };

        Exception? lastEx = null;
        foreach (var url in urls)
        {
            try
            {
                AppLogger.Info($"尝试下载 OCR 模型: {url}");
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(trainedDataPath, bytes);
                if (IsLikelyValidTrainedData(trainedDataPath))
                {
                    AppLogger.Info("OCR 模型下载并校验通过");
                    return;
                }

                AppLogger.Error("OCR 模型下载完成但校验失败，将尝试备用地址");
            }
            catch (Exception ex)
            {
                lastEx = ex;
                AppLogger.Error($"OCR 模型下载失败: {url}", ex);
            }
        }

        throw new InvalidOperationException("OCR 模型下载失败，请检查网络后重试。", lastEx);
    }

    private static int CountClues(int[,] board)
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
}
