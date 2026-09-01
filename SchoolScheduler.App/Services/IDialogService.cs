namespace SchoolScheduler.App.Services;

public interface IDialogService
{
    void ShowMessage(string title, string message);
    void ShowError(string message);
}