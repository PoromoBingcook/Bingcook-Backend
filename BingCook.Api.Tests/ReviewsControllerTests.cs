using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Controllers;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Reviews;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class ReviewsControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PropertyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReviewId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime CreatedAt = new(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMine_ReturnsNoContentWhenUserHasNotReviewedProperty()
    {
        var repository = new FakeReviewRepository();
        var controller = CreateController(repository);

        var action = await controller.GetMine(PropertyId, CancellationToken.None);

        Assert.IsType<NoContentResult>(action.Result);
        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(PropertyId, repository.LastPropertyId);
    }

    [Fact]
    public async Task GetMine_ReturnsAuthenticatedUsersReview()
    {
        var repository = new FakeReviewRepository
        {
            Existing = CreateReview(4, "Comfortable room.")
        };
        var controller = CreateController(repository);

        var action = await controller.GetMine(PropertyId, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ReviewResponse>(result.Value);
        Assert.Equal(ReviewId, response.Id);
        Assert.Equal(PropertyId, response.PropertyId);
        Assert.Equal(4, response.Rating);
        Assert.Equal("Comfortable room.", response.Comment);
        Assert.Equal(CreatedAt, response.CreatedAt);
    }

    [Fact]
    public async Task Upsert_TrimsCommentAndUsesAuthenticatedUser()
    {
        var repository = new FakeReviewRepository
        {
            Saved = CreateReview(5, "Helpful stay")
        };
        var controller = CreateController(repository);

        var action = await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(5, "  Helpful stay  "),
            CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ReviewResponse>(result.Value);
        Assert.Equal(5, response.Rating);
        Assert.Equal("Helpful stay", response.Comment);
        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(PropertyId, repository.LastPropertyId);
        Assert.Equal(5, repository.LastRating);
        Assert.Equal("Helpful stay", repository.LastComment);
    }

    [Fact]
    public async Task Upsert_StoresWhitespaceOnlyCommentAsNull()
    {
        var repository = new FakeReviewRepository
        {
            Saved = CreateReview(3, null)
        };
        var controller = CreateController(repository);

        await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(3, "   "),
            CancellationToken.None);

        Assert.Null(repository.LastComment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Upsert_RejectsRatingOutsideOneToFive(int rating)
    {
        var repository = new FakeReviewRepository();
        var controller = CreateController(repository);

        var action = await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(rating, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Null(repository.LastRating);
    }

    [Fact]
    public async Task Upsert_RejectsCommentLongerThanOneThousandCharacters()
    {
        var repository = new FakeReviewRepository();
        var controller = CreateController(repository);

        var action = await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(5, new string('a', 1001)),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Null(repository.LastRating);
    }

    [Fact]
    public async Task Upsert_ReturnsNotFoundWhenPropertyDoesNotExist()
    {
        var repository = new FakeReviewRepository { PropertyExists = false };
        var controller = CreateController(repository);

        var action = await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(5, null),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task Upsert_ReturnsUnauthorizedForInvalidUserClaim()
    {
        var repository = new FakeReviewRepository();
        var controller = CreateController(repository, "not-a-guid");

        var action = await controller.Upsert(
            PropertyId,
            new UpsertReviewRequest(5, null),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(action.Result);
        Assert.Null(repository.LastUserId);
    }

    private static ReviewsController CreateController(
        IReviewRepository repository,
        string? userId = null)
    {
        var controller = new ReviewsController(repository);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim(JwtRegisteredClaimNames.Sub, userId ?? UserId.ToString())],
                        "Test"))
            }
        };
        return controller;
    }

    private static UserReview CreateReview(int rating, string? comment)
    {
        return new UserReview(
            ReviewId,
            UserId,
            PropertyId,
            rating,
            comment,
            CreatedAt);
    }

    private sealed class FakeReviewRepository : IReviewRepository
    {
        public UserReview? Existing { get; init; }
        public UserReview? Saved { get; init; }
        public bool PropertyExists { get; init; } = true;
        public Guid? LastUserId { get; private set; }
        public Guid? LastPropertyId { get; private set; }
        public int? LastRating { get; private set; }
        public string? LastComment { get; private set; }

        public Task<UserReview?> GetMineAsync(
            Guid userId,
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastPropertyId = propertyId;
            return Task.FromResult(Existing);
        }

        public Task<ReviewUpsertResult> UpsertAsync(
            Guid userId,
            Guid propertyId,
            int rating,
            string? comment,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastPropertyId = propertyId;
            LastRating = rating;
            LastComment = comment;
            return Task.FromResult(
                PropertyExists
                    ? ReviewUpsertResult.Success(
                        Saved ?? CreateReview(rating, comment))
                    : ReviewUpsertResult.PropertyNotFound());
        }
    }
}
