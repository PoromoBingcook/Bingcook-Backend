using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Reviews;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private const int MaximumCommentLength = 1000;
    private readonly IReviewRepository _reviewRepository;
    private readonly IMultiReviewRepository? _multiReviewRepository;

    public ReviewsController(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
        _multiReviewRepository = reviewRepository as IMultiReviewRepository;
    }

    [HttpGet("properties/{propertyId:guid}/mine/all")]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> GetMineAll(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });
        if (_multiReviewRepository is null) return StatusCode(StatusCodes.Status501NotImplemented);
        var reviews = await _multiReviewRepository.GetMineAllAsync(userId.Value, propertyId, cancellationToken);
        return Ok(reviews.Select(ToResponse).ToArray());
    }

    [HttpPost("properties/{propertyId:guid}")]
    public async Task<ActionResult<ReviewResponse>> Create(
        Guid propertyId,
        UpsertReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });
        if (_multiReviewRepository is null) return StatusCode(StatusCodes.Status501NotImplemented);
        var validation = Validate(request, out var comment);
        if (validation is not null) return validation;
        var result = await _multiReviewRepository.CreateAsync(
            userId.Value, propertyId, request.Rating, comment, cancellationToken);
        if (!result.PropertyExists || result.Review is null)
            return NotFound(new { message = "Property not found." });
        return CreatedAtAction(nameof(GetMineAll), new { propertyId }, ToResponse(result.Review));
    }

    [HttpPut("{reviewId:guid}")]
    public async Task<ActionResult<ReviewResponse>> UpdateMine(
        Guid reviewId,
        UpsertReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });
        if (_multiReviewRepository is null) return StatusCode(StatusCodes.Status501NotImplemented);
        var validation = Validate(request, out var comment);
        if (validation is not null) return validation;
        var review = await _multiReviewRepository.UpdateMineAsync(
            userId.Value, reviewId, request.Rating, comment, cancellationToken);
        return review is null
            ? NotFound(new { message = "Review not found." })
            : Ok(ToResponse(review));
    }

    [HttpGet("properties/{propertyId:guid}/mine")]
    public async Task<ActionResult<ReviewResponse>> GetMine(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var review = await _reviewRepository.GetMineAsync(
            userId.Value,
            propertyId,
            cancellationToken);
        if (review is null)
        {
            return NoContent();
        }

        return Ok(ToResponse(review));
    }

    [HttpPut("properties/{propertyId:guid}")]
    public async Task<ActionResult<ReviewResponse>> Upsert(
        Guid propertyId,
        UpsertReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        }

        var comment = request.Comment?.Trim();
        if (string.IsNullOrEmpty(comment))
        {
            comment = null;
        }

        if (comment?.Length > MaximumCommentLength)
        {
            return BadRequest(new
            {
                message = $"Review comment cannot exceed {MaximumCommentLength} characters."
            });
        }

        var result = await _reviewRepository.UpsertAsync(
            userId.Value,
            propertyId,
            request.Rating,
            comment,
            cancellationToken);
        if (!result.PropertyExists || result.Review is null)
        {
            return NotFound(new { message = "Property not found." });
        }

        return Ok(ToResponse(result.Review));
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : null;
    }

    private ActionResult? Validate(UpsertReviewRequest request, out string? comment)
    {
        comment = request.Comment?.Trim();
        if (string.IsNullOrEmpty(comment)) comment = null;
        if (request.Rating is < 1 or > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        if (comment?.Length > MaximumCommentLength)
            return BadRequest(new { message = $"Review comment cannot exceed {MaximumCommentLength} characters." });
        return null;
    }

    private static ReviewResponse ToResponse(UserReview review)
    {
        return new ReviewResponse(
            review.Id,
            review.PropertyId,
            review.Rating,
            review.Comment,
            review.CreatedAt);
    }
}
