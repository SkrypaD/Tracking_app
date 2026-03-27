// <copyright file="ErrorHandlingMiddleware.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Serilog.Context;

namespace CartridgeApp.API.Middleware;

/// <summary>
/// Global middleware that catches all unhandled exceptions, assigns a unique ErrorId,
/// logs them with full context, and returns a localised, user-friendly JSON response.
/// </summary>
/// <remarks>
/// Error localisation uses <see cref="IStringLocalizer{T}"/> with resource files:
/// <c>Resources/Errors.uk.resx</c> (Ukrainian) and <c>Resources/Errors.en.resx</c> (English).
/// The active language is determined by the <c>Accept-Language</c> request header.
/// </remarks>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ErrorHandlingMiddleware> logger;
    private readonly IStringLocalizerFactory localizerFactory;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IStringLocalizerFactory localizerFactory)
    {
        this.next = next;
        this.logger = logger;
        this.localizerFactory = localizerFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await this.next(context);
        }
        catch (Exception ex)
        {
            await this.HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Unique identifier for this specific error occurrence — links logs to API response
        var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var (statusCode, userMessageKey, logLevel) = ex switch
        {
            KeyNotFoundException         => (HttpStatusCode.NotFound,            "Error.NotFound",        LogLevel.Warning),
            UnauthorizedAccessException  => (HttpStatusCode.Unauthorized,         "Error.Unauthorized",    LogLevel.Warning),
            InvalidOperationException    => (HttpStatusCode.BadRequest,           "Error.BadRequest",      LogLevel.Warning),
            ArgumentException            => (HttpStatusCode.BadRequest,           "Error.BadRequest",      LogLevel.Warning),
            OperationCanceledException   => (HttpStatusCode.ServiceUnavailable,   "Error.Cancelled",       LogLevel.Information),
            _                            => (HttpStatusCode.InternalServerError,  "Error.Internal",        LogLevel.Error),
        };

        // Push ErrorId into Serilog's LogContext so it appears in every log sink
        using (LogContext.PushProperty("ErrorId", errorId))
        {
            var logMessage =
                "Unhandled exception [{ErrorId}] {ExceptionType}: {ExceptionMessage} | " +
                "Path={Path} Method={Method} UserId={UserId}";

            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? "anonymous";

            this.logger.Log(
                logLevel,
                ex,
                logMessage,
                errorId,
                ex.GetType().Name,
                ex.Message,
                context.Request.Path,
                context.Request.Method,
                userId);
        }

        // Localise the user-facing message
        var localizer = this.localizerFactory.Create(
            "Errors", typeof(ErrorHandlingMiddleware).Assembly.GetName().Name!);

        // For known domain errors (NotFound, Unauthorized, BadRequest), include the
        // original exception message — it's already intended to be user-readable.
        // For internal errors, show only a generic localised message.
        var isUserFacingException = statusCode is HttpStatusCode.NotFound
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.BadRequest;

        var userMessage = isUserFacingException
            ? ex.Message
            : localizer[userMessageKey].Value;

        var response = new ErrorResponse(
            ErrorId: errorId,
            Status: (int)statusCode,
            Message: userMessage,
            Detail: localizer[$"{userMessageKey}.Detail"].Value,
            SuggestedAction: localizer[$"{userMessageKey}.Action"].Value);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Structured error response returned to the API consumer.
/// </summary>
/// <param name="ErrorId">Short unique identifier (8 hex chars) for correlating with server logs.</param>
/// <param name="Status">HTTP status code mirrored in the body for clients that cannot read headers.</param>
/// <param name="Message">Human-readable error description in the request language.</param>
/// <param name="Detail">Additional context about when/why this error occurs.</param>
/// <param name="SuggestedAction">What the user or developer can do to resolve the issue.</param>
public record ErrorResponse(
    string ErrorId,
    int Status,
    string Message,
    string Detail,
    string SuggestedAction);