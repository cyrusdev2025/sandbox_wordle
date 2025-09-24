using Microsoft.AspNetCore.SignalR;

// SignalR Hub for real-time multiplayer communication
public class MultiplayerHub : Hub
{
    private readonly IMultiplayerService _multiplayerService;
    private readonly ILogger<MultiplayerHub> _logger;

    public MultiplayerHub(IMultiplayerService multiplayerService, ILogger<MultiplayerHub> logger)
    {
        _multiplayerService = multiplayerService;
        _logger = logger;
    }

    // Join a game group
    public async Task JoinGame(string gameId, string playerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        
        // Update player's connection ID
        await _multiplayerService.UpdatePlayerConnectionAsync(gameId, playerId, Context.ConnectionId);
        
        _logger.LogInformation("Player {PlayerId} joined game {GameId} with connection {ConnectionId}", 
            playerId, gameId, Context.ConnectionId);
    }

    // Leave a game group
    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        _logger.LogInformation("Connection {ConnectionId} left game {GameId}", Context.ConnectionId, gameId);
    }

    // Handle player disconnection
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Player with connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
