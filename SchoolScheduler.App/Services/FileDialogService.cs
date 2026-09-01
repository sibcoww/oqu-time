using Microsoft.Win32;

namespace SchoolScheduler.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string ExcelFilter = "Excel (*.xlsx)|*.xlsx";
    public string? ChooseExcelSavePath(string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = ExcelFilter, FileName = defaultFileName, AddExtension = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? ChooseExcelOpenPath()
    {
        var dialog = new OpenFileDialog { Filter = ExcelFilter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
