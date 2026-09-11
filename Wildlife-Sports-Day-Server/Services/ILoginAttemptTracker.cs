namespace Wildlife_Sports_Day_Server.Services;

public interface ILoginAttemptTracker
{
    bool IsBlocked(string key);
    void RecordFailure(string key);
    void Reset(string key);
}
