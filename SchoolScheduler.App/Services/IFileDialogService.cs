namespace SchoolScheduler.App.Services;

public interface IFileDialogService
{
    string? ChooseExcelSavePath(string defaultFileName);
    string? ChooseExcelOpenPath();
}
