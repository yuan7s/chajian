using System.Windows.Forms;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 在 SW 主线程弹出原生 .swp 文件选择框，返回选中的真实本地路径。
    /// 浏览器无法拿到本地文件真实路径，故由插件侧弹框。
    /// </summary>
    private object PickSwpMacro()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 SolidWorks 宏",
            Filter = "SolidWorks 宏 (*.swp)|*.swp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return new { cancelled = true, path = "" };

        return new { cancelled = false, path = dialog.FileName };
    }
}
