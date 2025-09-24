var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IMultiplayerService, MultiplayerService>();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
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
app.MapPost("/start", async (StartGameRequest? request, IGameSessionService gameService) =>
{
    try
    {
        var response = await gameService.StartGameAsync(request);
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

// Multiplayer API Endpoints
app.MapPost("/multiplayer/join", async (JoinGameRequest request, IMultiplayerService multiplayerService) =>
{
    try
    {
        var response = await multiplayerService.JoinGameAsync(request);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to join game: {ex.Message}");
    }
});

app.MapGet("/multiplayer/game/{gameId}", async (string gameId, IMultiplayerService multiplayerService) =>
{
    try
    {
        var gameState = await multiplayerService.GetGameStateAsync(gameId);
        return gameState != null ? Results.Ok(gameState) : Results.NotFound("Game not found");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get game state: {ex.Message}");
    }
});

app.MapPost("/multiplayer/guess", async (MultiplayerGuessRequest request, IMultiplayerService multiplayerService) =>
{
    try
    {
        var response = await multiplayerService.SubmitGuessAsync(request);
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

// Map SignalR Hub
app.MapHub<MultiplayerHub>("/multiplayerhub");

app.Run();
