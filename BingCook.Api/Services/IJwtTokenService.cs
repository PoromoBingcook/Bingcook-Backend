using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IJwtTokenService
{
    string CreateToken(UserAccount user);
}
