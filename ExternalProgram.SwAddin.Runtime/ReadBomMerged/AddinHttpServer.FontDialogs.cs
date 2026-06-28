using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SldWorks;

namespace ReadBom.SwAddin;

internal sealed partial class AddinHttpServer
{
    private ModelDoc2 OpenDoc6WithDialogHandling(
        string path,
        int docType,
        int options,
        string configuration,
        ref int errors,
        ref int warnings)
    {
        using var fontDialogHandler = new SolidWorksFontDialogWatcher(TimeSpan.FromSeconds(60));
        var model = _swApp.OpenDoc6(path, docType, options, configuration ?? string.Empty, ref errors, ref warnings)
            as ModelDoc2;
        if (fontDialogHandler.HandledCount > 0)
            AddinLog.Write("OpenDoc6 temporary font replacement dialogs handled: " +
                           fontDialogHandler.HandledCount + ", path=" + path);

        return model;
    }

    private sealed class SolidWorksFontDialogWatcher : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _handledCount;

        public SolidWorksFontDialogWatcher(TimeSpan maxDuration)
        {
            Task.Run(() => RunAsync(maxDuration, _cts.Token));
        }

        public int HandledCount => Volatile.Read(ref _handledCount);

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                AddinLog.Write("SolidWorksFontDialogWatcher.Dispose ignored: " + ex.Message);
            }
        }

        private async Task RunAsync(TimeSpan maxDuration, CancellationToken token)
        {
            var stopAtUtc = DateTime.UtcNow + maxDuration;
            while (!token.IsCancellationRequested && DateTime.UtcNow < stopAtUtc)
            {
                try
                {
                    if (TryDismissSolidWorksFontDialog())
                        Interlocked.Increment(ref _handledCount);
                }
                catch (Exception ex)
                {
                    AddinLog.Write("SolidWorksFontDialogWatcher ignored: " + ex.Message);
                }

                try
                {
                    await Task.Delay(150, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static bool TryDismissSolidWorksFontDialog()
    {
        var targetProcessId = Process.GetCurrentProcess().Id;
        var handled = false;
        EnumWindows((window, _) =>
        {
            if (handled) return false;
            if (!IsWindowVisible(window)) return true;

            GetWindowThreadProcessId(window, out var processId);
            if (processId != targetProcessId) return true;

            var windowText = GetWindowTextValue(window);
            var childTexts = GetChildWindowTexts(window);
            var dialogText = windowText + "\n" + string.Join("\n", childTexts);
            if (!LooksLikeMissingFontDialog(dialogText)) return true;

            var button = FindChildWindow(window, IsTemporaryFontReplacementButton);
            if (button == IntPtr.Zero) return true;

            SendMessage(button, BmClick, IntPtr.Zero, IntPtr.Zero);
            AddinLog.Write("SolidWorks missing font dialog handled with temporary font replacement: " + windowText);
            handled = true;
            return false;
        }, IntPtr.Zero);

        return handled;
    }

    private static bool LooksLikeMissingFontDialog(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var hasFont = ContainsIgnoreCase(text, "字体") || ContainsIgnoreCase(text, "font");
        if (!hasFont) return false;

        var hasMissingText =
            ContainsIgnoreCase(text, "未安装") ||
            ContainsIgnoreCase(text, "缺失") ||
            ContainsIgnoreCase(text, "找不到") ||
            ContainsIgnoreCase(text, "not installed") ||
            ContainsIgnoreCase(text, "missing") ||
            ContainsIgnoreCase(text, "not found");
        var hasTemporaryReplacement =
            ContainsIgnoreCase(text, "临时") && ContainsIgnoreCase(text, "替换") ||
            ContainsIgnoreCase(text, "temporary") && (ContainsIgnoreCase(text, "replace") ||
                                                      ContainsIgnoreCase(text, "substitute"));

        return hasMissingText || hasTemporaryReplacement;
    }

    private static bool IsTemporaryFontReplacementButton(IntPtr window)
    {
        if (!string.Equals(GetWindowClassName(window), "Button", StringComparison.OrdinalIgnoreCase))
            return false;

        var text = GetWindowTextValue(window);
        if (string.IsNullOrWhiteSpace(text)) return false;

        return ContainsIgnoreCase(text, "临时") && ContainsIgnoreCase(text, "替换") ||
               ContainsIgnoreCase(text, "temporary") && (ContainsIgnoreCase(text, "replace") ||
                                                         ContainsIgnoreCase(text, "substitute")) ||
               ContainsIgnoreCase(text, "temporarily") && ContainsIgnoreCase(text, "font");
    }

    private static IntPtr FindChildWindow(IntPtr parent, Func<IntPtr, bool> predicate)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parent, (child, _) =>
        {
            if (predicate(child))
            {
                result = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static string[] GetChildWindowTexts(IntPtr parent)
    {
        var texts = new List<string>();
        EnumChildWindows(parent, (child, _) =>
        {
            var text = GetWindowTextValue(child);
            if (!string.IsNullOrWhiteSpace(text))
                texts.Add(text);
            return true;
        }, IntPtr.Zero);
        return texts.ToArray();
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return value?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetWindowTextValue(IntPtr window)
    {
        var builder = new StringBuilder(512);
        GetWindowText(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetWindowClassName(IntPtr window)
    {
        var builder = new StringBuilder(128);
        GetClassName(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private const int BmClick = 0x00F5;

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
