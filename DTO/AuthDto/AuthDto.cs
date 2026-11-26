using p48_lessons_asp.DTO.User;

namespace p48_lessons_asp.DTO.AuthDto;

public class AuthDto
{
    public UserDto User { get; set; } = null!;
    public object? Tokens { get; set; }
}