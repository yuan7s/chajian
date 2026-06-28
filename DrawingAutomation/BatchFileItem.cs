using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ExternalProgram;

/// <summary>
/// View-model for one file row in the batch file filter dialog.
/// </summary>
public sealed class BatchFileItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string DocType { get; set; } = "";

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static BatchFileItem FromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToUpperInvariant();
        var docType = ext switch
        {
            ".SLDPRT" => "零件",
            ".SLDASM" => "装配体",
            _ => ext ?? ""
        };
        return new BatchFileItem
        {
            FileName = Path.GetFileName(path),
            FullPath = path,
            DocType = docType
        };
    }
}
