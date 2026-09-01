namespace SchoolScheduler.App.Services;

public interface IFileDialogService
{
    string? ChooseExcelSavePath(string defaultFileName);
    string? ChooseExcelOpenPath();
    string? ChooseBackupSavePath(string defaultFileName);
    string? ChooseBackupOpenPath();
    string? ChoosePdfSavePath(string defaultFileName);
}
