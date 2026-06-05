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

    private enum OcrBackend { OneOcr, WinMedia, None }
    private static OcrBackend _backend = OcrBackend.None;
    private static OcrEngine? _ocrEngine;

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { Console.InputEncoding = Encoding.UTF8; } catch { /* redirected stdin */ }

        // OneOCR 모델 디렉터리: argv[0] 우선, 없으면 env (Node가 Get-AppxPackage로 해석해 전달).
        string oneocrDir = (args.Length > 0 ? args[0] : null) ?? "";
        if (string.IsNullOrWhiteSpace(oneocrDir))
            oneocrDir = Environment.GetEnvironmentVariable("HD2_ONEOCR_DIR") ?? "";

        // 1순위 엔진: OneOCR(캡처도구 엔진). 비공식 ABI이므로 어떤 실패든 catch → 폴백.
        if (!string.IsNullOrWhiteSpace(oneocrDir))
        {
            try { if (OneOcrEngine.Init(oneocrDir)) _backend = OcrBackend.OneOcr; }
            catch { _backend = OcrBackend.None; }
            if (_backend != OcrBackend.OneOcr)
                Console.Error.WriteLine("[oneocr-init] " + (OneOcrEngine.LastError ?? "unknown"));
        }

        // 폴백: Windows.Media.Ocr (장비 자동장착 검증 경로). 워밍업 (레퍼런스 WarmupOcr)
        if (_backend != OcrBackend.OneOcr)
        {
            try
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ko-KR"));
                if (_ocrEngine != null)
                {
                    using var dummy = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
                    await _ocrEngine.RecognizeAsync(dummy);
                    _backend = OcrBackend.WinMedia;
                }
            }
            catch { _ocrEngine = null; }
        }

        // 준비 신호 (Node가 첫 줄로 엔진 종류까지 확인): READY ONEOCR / READY WINMEDIA / NO_OCR
        Console.WriteLine(_backend == OcrBackend.OneOcr ? "READY ONEOCR"
            : _backend == OcrBackend.WinMedia ? "READY WINMEDIA" : "NO_OCR");
        Console.Out.Flush();

        // 셀프테스트: `<oneocrDir> --selftest <imagePath>` → 이미지 파일을 직접 인식(게임 불필요).
        if (args.Length >= 3 && args[1] == "--selftest")
        {
            try
            {
                using var bmp = new Bitmap(args[2]);
                using var rgba = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppArgb);
                var d = rgba.LockBits(new Rectangle(0, 0, rgba.Width, rgba.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = Math.Abs(d.Stride);
                byte[] buf = new byte[stride * rgba.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                rgba.UnlockBits(d);
                Console.WriteLine("SELFTEST[" + _backend + "]: " + OneOcrEngine.Recognize(buf, rgba.Width, rgba.Height, stride));
            }
            catch (Exception ex) { Console.WriteLine("SELFTEST_ERR: " + ex.Message); }
            Console.Out.Flush();
            return;
        }

        // 쿨다운 셀프테스트: `<oneocrDir> --cdtest <imagePath> [탭구분 후보들]` → 줄/페어 출력.
        if (args.Length >= 3 && args[1] == "--cdtest")
        {
            try
            {
                using var bmp = new Bitmap(args[2]);
                using var rgba = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppArgb);
                var d = rgba.LockBits(new Rectangle(0, 0, rgba.Width, rgba.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = Math.Abs(d.Stride);
                byte[] buf = new byte[stride * rgba.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                rgba.UnlockBits(d);
                var lines = OneOcrEngine.RecognizeLines(buf, rgba.Width, rgba.Height, stride);
                string[] cands = (args.Length >= 4 && File.Exists(args[3]))
                    ? File.ReadAllLines(args[3]).Where(s => s.Trim().Length > 0).ToArray()
                    : new string[0];
                Console.WriteLine("CDTEST lines: " + string.Join(" | ", lines));
                Console.WriteLine("CDTEST pairs: " + PairCooldowns(lines, cands));
            }
            catch (Exception ex) { Console.WriteLine("CDTEST_ERR: " + ex.Message); }
            Console.Out.Flush();
            return;
        }

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (line.Length == 0) { Console.WriteLine(""); Console.Out.Flush(); continue; }
            string result = "";
            try
            {
                if (line.StartsWith("COOLDOWNS\t"))
                {
                    // 쿨다운 OCR: 후보 = 인게임 한글 잼 이름들. 응답 = "한글이름=초|..."
                    string[] cands = line.Split('\t').Skip(1).ToArray();
                    result = MatchCooldownsFromScreen(cands);
                }
                else
                {
                    string[] candidates = line.Split('\t');
                    result = await MatchItemFromScreen(candidates) ?? "";
                }
            }
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

        double scale = 3.5;
        int resizedW = (int)Math.Round(cap.Width * scale);
        int resizedH = (int)Math.Round(cap.Height * scale);

        using var resized = new Bitmap(resizedW, resizedH, PixelFormat.Format32bppArgb);
        using (var rg = Graphics.FromImage(resized))
        {
            rg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            rg.DrawImage(cap, 0, 0, resized.Width, resized.Height);
        }

        string rawText = "";
        if (_backend == OcrBackend.OneOcr)
        {
            // OneOCR: 전처리 없이 업스케일된 BGRA를 그대로 인식 (흐린 이름까지 정확).
            var bd = resized.LockBits(new Rectangle(0, 0, resized.Width, resized.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(bd.Stride);
                byte[] bgra = new byte[stride * resized.Height];
                Marshal.Copy(bd.Scan0, bgra, 0, bgra.Length);
                rawText = OneOcrEngine.Recognize(bgra, resized.Width, resized.Height, stride);
            }
            finally { resized.UnlockBits(bd); }
        }
        else
        {
            // Windows.Media.Ocr 폴백: 흰픽셀>165 마스크 + dilation 전처리 (검증된 경로).
            double radius = 3.95;
            int pad = 90;
            int limit = (int)Math.Ceiling(radius);

            var offsets = new List<(int dx, int dy)>();
            for (int ky = -limit; ky <= limit; ky++)
                for (int kx = -limit; kx <= limit; kx++)
                    if (Math.Sqrt(kx * kx + ky * ky) <= radius) offsets.Add((kx, ky));

            int finalW = resized.Width + (pad * 2);
            int finalH = resized.Height + (pad * 2);

            using var finalBmp = new Bitmap(finalW, finalH, PixelFormat.Format32bppArgb);
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

    // 쿨다운 OCR: 잼 메뉴(좌하단) 영역을 캡처해 줄별로 (이름, 쿨다운 T-MM:SS)를 페어링.
    // candidates = 인게임 한글 잼 이름들. 반환 = "한글이름=초|..." (준비=0). OneOCR 전용.
    private static string MatchCooldownsFromScreen(string[] candidates)
    {
        if (_backend != OcrBackend.OneOcr) return "";
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "";
        var cn = new StringBuilder(256);
        GetClassName(hwnd, cn, cn.Capacity);
        if (cn.ToString() != "stingray_window") return "";
        if (!GetClientRect(hwnd, out RECT cr)) return "";
        int cw = cr.Right - cr.Left, chh = cr.Bottom - cr.Top;
        if (cw <= 0 || chh <= 0) return "";

        // 잼 메뉴: 좌상단(0,0) 원점, 1440p 높이 기준 가로400×세로1000 박스.
        // 화면비 무관(좌상단 고정) → 클라이언트 높이로만 스케일. env(HD2_CD_*)는 1440p 픽셀값(미세조정용).
        double sc = chh / 1440.0;
        double bx = CdFrac("HD2_CD_X", 0.0), by = CdFrac("HD2_CD_Y", 0.0);
        double bw = CdFrac("HD2_CD_W", 400.0), bh = CdFrac("HD2_CD_H", 1000.0);
        POINT sp = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref sp);
        var region = new Rectangle(
            sp.X + (int)Math.Round(bx * sc), sp.Y + (int)Math.Round(by * sc),
            (int)Math.Round(bw * sc), (int)Math.Round(bh * sc));
        if (region.Width < 50 || region.Height < 50) return "";

        using var cap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cap))
            g.CopyFromScreen(region.Left, region.Top, 0, 0, cap.Size);
        if (Environment.GetEnvironmentVariable("HD2_CD_DEBUG") == "1")
            try { cap.Save(Path.Combine(Path.GetTempPath(), "hd2cd.png"), ImageFormat.Png); } catch { }

        double scale = 2.0;
        int rw = (int)Math.Round(cap.Width * scale), rh = (int)Math.Round(cap.Height * scale);
        using var resized = new Bitmap(rw, rh, PixelFormat.Format32bppArgb);
        using (var rg = Graphics.FromImage(resized))
        {
            rg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            rg.DrawImage(cap, 0, 0, rw, rh);
        }
        List<string> lines;
        var bd = resized.LockBits(new Rectangle(0, 0, rw, rh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = Math.Abs(bd.Stride);
            byte[] bgra = new byte[stride * rh];
            Marshal.Copy(bd.Scan0, bgra, 0, bgra.Length);
            lines = OneOcrEngine.RecognizeLines(bgra, rw, rh, stride);
        }
        finally { resized.UnlockBits(bd); }

        return PairCooldowns(lines, candidates);
    }

    // 줄 리스트 위→아래: 상태줄(쿨다운/준비/타이머)을 직전 한글 이름줄과 페어링 → "이름=초|...".
    // 한글 줄만 이름으로 인정(아이콘 배지의 숫자/기호 junk 가 이름을 덮지 않게).
    private static string PairCooldowns(List<string> lines, string[] candidates)
    {
        var results = new List<string>();
        string? lastName = null;
        foreach (var raw in lines)
        {
            string t = (raw ?? "").Trim();
            if (t.Length == 0) continue;
            // 상태줄 분류. "접근 중 T-MM:SS"(콜인/도착 중)는 쿨타임이 아니므로 제외, "쿨다운"만 적용.
            bool isApproaching = t.Contains("접근");
            bool isReady = t.Contains("준비");
            bool hasTimer = t.Contains("T-") || Regex.IsMatch(t, @"\d{1,2}\s*[:：]\s*\d{2}");
            bool isCooldown = !isApproaching && !isReady && (t.Contains("쿨다운") || hasTimer);
            if (isApproaching || isReady || isCooldown)
            {
                if (lastName != null)
                {
                    // 접근 중은 이름만 소비하고 결과에 넣지 않음(쿨타임 미적용).
                    if (isCooldown || isReady)
                    {
                        string? matched = BestMatch(lastName, candidates, 0.5);
                        if (matched != null) results.Add(matched + "=" + (isReady ? 0 : ParseTimerSeconds(t)));
                    }
                    lastName = null;
                }
            }
            else if (Regex.IsMatch(t, "[가-힣]")) lastName = t;
        }
        return string.Join("|", results);
    }

    private static double CdFrac(string env, double def)
    {
        var v = Environment.GetEnvironmentVariable(env);
        if (v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return def;
    }

    // 숫자만 추출 → 끝 4자리 = MMSS → 초 (T→1 오인식·콜론 누락 흡수).
    private static int ParseTimerSeconds(string t)
    {
        string digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return 0;
        if (digits.Length > 4) digits = digits.Substring(digits.Length - 4);
        digits = digits.PadLeft(4, '0');
        return int.Parse(digits.Substring(0, 2)) * 60 + int.Parse(digits.Substring(2, 2));
    }

    // OCR 이름을 후보와 Levenshtein 매칭(Repair/Clean 재사용). minSim 미만이면 null.
    private static string? BestMatch(string ocrName, string[] candidates, double minSim)
    {
        string cleanOCR = Clean(Repair(ocrName));
        if (cleanOCR.Length == 0) return null;
        string? best = null; double bestSim = -1.0;
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            string cleanDB = Clean(Repair(name));
            int dist = GetLevenshteinDistance(cleanOCR, cleanDB);
            double sim = 1.0 - ((double)dist / Math.Max(cleanOCR.Length, Math.Max(cleanDB.Length, 1)));
            if (sim > bestSim) { bestSim = sim; best = name; }
        }
        return bestSim >= minSim ? best : null;
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

// OneOCR(캡처도구 oneocr.dll) P/Invoke 래퍼. 비공식 RE된 C ABI이므로 모든 호출을 여기 격리.
// python `oneocr` 래퍼(키/구조체/함수 시그니처)를 C#으로 포팅.
internal static class OneOcrEngine
{
    private const string DLL = "oneocr.dll";
    // oneocr.dll 의 하드코딩 모델 키 (null 종료).
    private static readonly byte[] KEY = Encoding.ASCII.GetBytes("kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4\0");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectoryW(string lpPathName);

    [StructLayout(LayoutKind.Sequential)]
    private struct ImageStruct
    {
        public int type;       // 3 = BGRA
        public int width;
        public int height;
        public int reserved;
        public long step;      // 행당 바이트수 (= width*4 또는 stride)
        public IntPtr data;    // BGRA 픽셀 포인터
    }

    [DllImport(DLL)] private static extern long CreateOcrInitOptions(out long opts);
    [DllImport(DLL)] private static extern long OcrInitOptionsSetUseModelDelayLoad(long opts, byte flag);
    [DllImport(DLL)] private static extern long CreateOcrPipeline(byte[] modelPath, byte[] key, long opts, out long pipeline);
    [DllImport(DLL)] private static extern long CreateOcrProcessOptions(out long popts);
    [DllImport(DLL)] private static extern long OcrProcessOptionsSetMaxRecognitionLineCount(long popts, long count);
    [DllImport(DLL)] private static extern long RunOcrPipeline(long pipeline, ref ImageStruct img, long popts, out long result);
    [DllImport(DLL)] private static extern long GetOcrLineCount(long result, out long count);
    [DllImport(DLL)] private static extern long GetOcrLine(long result, long index, out long line);
    [DllImport(DLL)] private static extern long GetOcrLineContent(long line, out IntPtr content);
    [DllImport(DLL)] private static extern void ReleaseOcrResult(long result);

    private static long _pipeline, _initOpts, _procOpts;
    private static bool _ready;
    public static string? LastError;

    // dir = oneocr.dll + oneocr.onemodel + onnxruntime.dll 가 있는 폴더.
    public static bool Init(string dir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir)) { LastError = "empty dir"; return false; }
            SetDllDirectoryW(dir); // 의존 onnxruntime.dll 을 동일 폴더에서 로드
            if (CreateOcrInitOptions(out _initOpts) != 0) { LastError = "CreateOcrInitOptions!=0"; return false; }
            OcrInitOptionsSetUseModelDelayLoad(_initOpts, 0);
            byte[] modelPath = Encoding.UTF8.GetBytes(Path.Combine(dir, "oneocr.onemodel") + "\0");
            long rc = CreateOcrPipeline(modelPath, KEY, _initOpts, out _pipeline);
            if (rc != 0) { LastError = "CreateOcrPipeline rc=" + rc; return false; }
            if (CreateOcrProcessOptions(out _procOpts) != 0) { LastError = "CreateOcrProcessOptions!=0"; return false; }
            OcrProcessOptionsSetMaxRecognitionLineCount(_procOpts, 1000);
            _ready = true;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    // BGRA 버퍼를 인식해 줄 텍스트 리스트(위→아래)를 반환. 실패 시 빈 리스트.
    public static List<string> RecognizeLines(byte[] bgra, int width, int height, int step)
    {
        var lines = new List<string>();
        if (!_ready) return lines;
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        try
        {
            var img = new ImageStruct
            {
                type = 3,
                width = width,
                height = height,
                reserved = 0,
                step = step,
                data = handle.AddrOfPinnedObject()
            };
            if (RunOcrPipeline(_pipeline, ref img, _procOpts, out long result) != 0) return lines;
            try
            {
                if (GetOcrLineCount(result, out long lineCount) != 0) return lines;
                for (long i = 0; i < lineCount; i++)
                {
                    if (GetOcrLine(result, i, out long line) != 0) continue;
                    if (GetOcrLineContent(line, out IntPtr content) == 0 && content != IntPtr.Zero)
                    {
                        string s = Marshal.PtrToStringUTF8(content) ?? "";
                        if (s.Length > 0) lines.Add(s);
                    }
                }
                return lines;
            }
            finally { ReleaseOcrResult(result); }
        }
        finally { handle.Free(); }
    }

    // 줄을 공백으로 이어 반환(장비 OCR용).
    public static string Recognize(byte[] bgra, int width, int height, int step)
        => string.Join(" ", RecognizeLines(bgra, width, height, step));
}
