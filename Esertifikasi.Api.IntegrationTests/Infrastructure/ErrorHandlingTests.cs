using System.Text.Json;
using Esertifikasi.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esertifikasi.Api.IntegrationTests.Infrastructure;

public sealed class ErrorHandlingTests {
  [Fact]
  public async Task DatabaseError_ReturnsSafeIndonesianProblemDetails() {
    var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
    var context = new DefaultHttpContext();
    context.Request.Path = "/api/test";
    context.Response.Body = new MemoryStream();

    var handled = await handler.TryHandleAsync(context, new DbUpdateException("sensitive database detail"), CancellationToken.None);
    context.Response.Body.Position = 0;
    var json = await JsonDocument.ParseAsync(context.Response.Body);

    Assert.True(handled);
    Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    Assert.Equal("Konflik data", json.RootElement.GetProperty("title").GetString());
    Assert.DoesNotContain("sensitive", json.RootElement.GetProperty("detail").GetString());
    Assert.True(json.RootElement.TryGetProperty("traceId", out _));
  }
}
