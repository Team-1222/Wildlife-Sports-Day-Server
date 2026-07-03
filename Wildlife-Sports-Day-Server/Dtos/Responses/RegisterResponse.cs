namespace Wildlife_Sports_Day_Server.Dtos.Responses;

public record RegisterResponse(int Id, string Email, string Nickname);
/*{
    public int Id { get; init; } 
    public string Email { get; init; } = string.Empty;
    public string Nickname { get; init;} = string.Empty;
}*/