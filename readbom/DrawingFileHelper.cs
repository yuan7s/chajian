using System.IO;

namespace ReadBom;

internal static class DrawingFileHelper
{
    public static bool HasSiblingDrawing(string modelPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(modelPath);
            var fileName = Path.GetFileNameWithoutExtension(modelPath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            if (!Directory.Exists(directory))
            {
                return false;
            }

            return Directory.EnumerateFiles(directory, "*.slddrw")
                .Any(file => string.Equals(Path.GetFileNameWithoutExtension(file), fileName,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
