namespace Wildlife_Sports_Day_Server.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
