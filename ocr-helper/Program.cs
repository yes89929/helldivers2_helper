// HD2 장비 자동장착용 OCR 헬퍼 (레퍼런스 Program.cs MatchItemFromScreen 1:1 포팅).
// 프로토콜: stdin 한 줄 = 탭(\t)으로 구분된 후보 이름들 → stdout 한 줄 = 매칭된 이름(없으면 빈 줄).
// 화면 캡처/전처리/OCR(ko-KR)/Repair/Clean/Levenshtein 은 레퍼런스와 동일.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

internal static class Program
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    private static OcrEngine? _ocrEngine;

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { Console.InputEncoding = Encoding.UTF8; } catch { /* redirected stdin */ }

        // OCR 엔진 준비 + 워밍업 (레퍼런스 WarmupOcr)
        try
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ko-KR"));
            if (_ocrEngine != null)
            {
                using var dummy = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
                await _ocrEngine.RecognizeAsync(dummy);
            }
        }
        catch { _ocrEngine = null; }

        // 준비 신호 (Node 측에서 첫 줄로 readiness 확인)
        Console.WriteLine(_ocrEngine != null ? "READY" : "NO_OCR");
        Console.Out.Flush();

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (line.Length == 0) { Console.WriteLine(""); Console.Out.Flush(); continue; }
            string[] candidates = line.Split('\t');
            string result = "";
            try { result = await MatchItemFromScreen(candidates) ?? ""; }
            catch { result = ""; }
            Console.WriteLine(result);
            Console.Out.Flush();
        }
    }

    private static async Task<string?> MatchItemFromScreen(string[] candidates)
    {
        // ── 레퍼런스 MatchItemFromScreen (Program.cs:1071-1290) 포팅 ──
        int rectX = 890, rectY = 580, rectW = 530, rectH = 35;

        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        var className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        if (className.ToString() != "stingray_window") return null;
        if (!GetClientRect(hwnd, out RECT clientRect)) return null;

        int cw = clientRect.Right - clientRect.Left;
        int chh = clientRect.Bottom - clientRect.Top;
        if (cw <= 0 || chh <= 0) return null;

        double currentAspect = (double)cw / chh;
        double targetAspect = 16.0 / 9.0;
        double finalRatio, offsetX = 0, offsetY = 0;
        if (currentAspect > targetAspect)
        {
            finalRatio = chh / 1080.0;
            offsetX = (cw - (1920.0 * finalRatio)) / 2.0;
        }
        else
        {
            finalRatio = cw / 1920.0;
            offsetY = (chh - (1080.0 * finalRatio)) / 2.0;
        }

        POINT startPoint = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref startPoint);

        var region = new Rectangle(
            startPoint.X + (int)Math.Round(rectX * finalRatio + offsetX),
            startPoint.Y + (int)Math.Round(rectY * finalRatio + offsetY),
            (int)Math.Round(rectW * finalRatio),
            (int)Math.Round(rectH * finalRatio));
        if (region.Width <= 0 || region.Height <= 0) return null;

        using var cap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cap))
            g.CopyFromScreen(region.Left, region.Top, 0, 0, cap.Size);

        double scale = 3.5, radius = 3.95;
        int pad = 90;
        int resizedW = (int)Math.Round(cap.Width * scale);
        int resizedH = (int)Math.Round(cap.Height * scale);
        int limit = (int)Math.Ceiling(radius);

        using var resized = new Bitmap(resizedW, resizedH, PixelFormat.Format32bppArgb);
        using (var rg = Graphics.FromImage(resized))
        {
            rg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            rg.DrawImage(cap, 0, 0, resized.Width, resized.Height);
        }

        var offsets = new List<(int dx, int dy)>();
        for (int ky = -limit; ky <= limit; ky++)
            for (int kx = -limit; kx <= limit; kx++)
                if (Math.Sqrt(kx * kx + ky * ky) <= radius) offsets.Add((kx, ky));

        int finalW = resized.Width + (pad * 2);
        int finalH = resized.Height + (pad * 2);

        string rawText = "";
        using (var finalBmp = new Bitmap(finalW, finalH, PixelFormat.Format32bppArgb))
        {
            BitmapData? srcData = null, dstData = null;
            try
            {
                srcData = resized.LockBits(new Rectangle(0, 0, resized.Width, resized.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                dstData = finalBmp.LockBits(new Rectangle(0, 0, finalW, finalH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int srcStride = Math.Abs(srcData.Stride);
                int dstStride = Math.Abs(dstData.Stride);
                byte[] srcPixels = new byte[srcStride * srcData.Height];
                byte[] dstPixels = new byte[dstStride * dstData.Height];
                Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);
                Array.Fill<byte>(dstPixels, 255);

                for (int y = 0; y < resized.Height; y++)
                {
                    for (int x = 0; x < resized.Width; x++)
                    {
                        int srcIdx = (y * srcStride) + (x * 4);
                        if (srcPixels[srcIdx + 0] > 165 && srcPixels[srcIdx + 1] > 165 && srcPixels[srcIdx + 2] > 165)
                        {
                            foreach (var (dx, dy) in offsets)
                            {
                                int outX = x + pad + dx;
                                int outY = y + pad + dy;
                                if (outX >= 0 && outX < finalW && outY >= 0 && outY < finalH)
                                {
                                    int dstIdx = (outY * dstStride) + (outX * 4);
                                    dstPixels[dstIdx + 0] = 0;
                                    dstPixels[dstIdx + 1] = 0;
                                    dstPixels[dstIdx + 2] = 0;
                                    dstPixels[dstIdx + 3] = 255;
                                }
                            }
                        }
                    }
                }
                Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
            }
            finally
            {
                if (srcData != null) resized.UnlockBits(srcData);
                if (dstData != null) finalBmp.UnlockBits(dstData);
            }

            using var ms = new MemoryStream();
            finalBmp.Save(ms, ImageFormat.Png);
            byte[] pngBytes = ms.ToArray();

            using var ras = new InMemoryRandomAccessStream();
            await ras.WriteAsync(pngBytes.AsBuffer());
            ras.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(ras);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            if (_ocrEngine == null)
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ko-KR"));
                if (_ocrEngine == null) return null;
            }
            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
            rawText = ocrResult.Text ?? "";
        }

        string cleanOCR = Clean(Repair(rawText));
        if (string.IsNullOrEmpty(cleanOCR)) return null;

        string? bestName = null;
        double bestSim = -1.0;
        foreach (var name in candidates)
        {
            string cleanDB = Clean(Repair(name));
            int distance = GetLevenshteinDistance(cleanOCR, cleanDB);
            double sim = 1.0 - ((double)distance / Math.Max(cleanOCR.Length, Math.Max(cleanDB.Length, 1)));
            if (sim > bestSim) { bestSim = sim; bestName = name; }
        }

        return (bestName != null && bestSim > 0.6) ? bestName : null;
    }

    // 레퍼런스 Repair (Program.cs:1229-1239) — 한글 OCR 오인식 보정. 순서 그대로.
    private static string Repair(string input) => input
        .Replace(")(", "X").Replace("卜", "I").Replace("ⅹ", "X")
        .Replace("ⅴ", "V").Replace("+", "B").Replace("l", "I")
        .Replace("불", "블").Replace("뱸", "뱀").Replace("르", "드")
        .Replace("엔", "맨").Replace("앤", "맨").Replace("멘", "맨")
        .Replace("책", "잭").Replace("피", "퍼").Replace("저", "처")
        .Replace("쳐", "처").Replace("적", "척").Replace("셀", "샐")
        .Replace("일", "열").Replace("진", "친").Replace("제", "체")
        .Replace("장", "창").Replace("04", "CM").Replace("21", "기")
        .Replace("G-23", "23").Replace("I", "1").Replace("O", "0");

    // 레퍼런스 Clean (Program.cs:1240)
    private static string Clean(string input) =>
        Regex.Replace(input, @"[^가-힣a-zA-Z0-9]", "").ToUpper();

    // 레퍼런스 GetLevenshteinDistance (Program.cs:1292-1310)
    private static int GetLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;
        int n = s.Length, m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
