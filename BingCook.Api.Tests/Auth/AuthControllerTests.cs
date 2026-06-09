using BingCook.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Tests.Auth;

public sealed class AuthControllerTests
{
    [Fact]
    public void Logout_requires_authorization_and_uses_post_logout_route()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(AuthorizeAttribute), false).SingleOrDefault());
        var post = Assert.IsType<HttpPostAttribute>(
            method.GetCustomAttributes(typeof(HttpPostAttribute), false).Single());
        Assert.Equal("logout", post.Template);
    }
}
