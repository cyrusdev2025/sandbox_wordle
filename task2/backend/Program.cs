var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGameSessionService, GameSessionService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use CORS
app.UseCors("AllowAngularApp");

// Comment out HTTPS redirection for development
// app.UseHttpsRedirection();

// Wordle Game API Endpoints
app.MapPost("/start", async (IGameSessionService gameService) =>
{
    try
    {
        var response = await gameService.StartNewGameAsync();
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to start game: {ex.Message}");
    }
});

app.MapGet("/validate/{sessionId}", async (string sessionId, IGameSessionService gameService) =>
{
    try
    {
        var response = await gameService.ValidateSessionAsync(sessionId);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to validate session: {ex.Message}");
    }
});

app.MapPost("/submit", async (SubmitGuessRequest request, IGameSessionService gameService) =>
{
    try
    {
        var response = await gameService.SubmitGuessAsync(request);
        return Results.Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to submit guess: {ex.Message}");
    }
});

app.Run();
