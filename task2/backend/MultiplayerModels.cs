// Multiplayer models for 2-player Wordle game

public class JoinGameRequest
{
    public string PlayerName { get; set; } = string.Empty;
}

public class JoinGameResponse
{
    public string GameId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public bool IsWaiting { get; set; } // true if waiting for second player
    public GameState? GameState { get; set; }
}

public class GameState
{
    public string GameId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = new();
    public string Status { get; set; } = "Waiting"; // Waiting, InProgress, Completed
    public string Answer { get; set; } = string.Empty;
    public int MaxTrials { get; set; } = 6;
    public DateTime CreatedAt { get; set; }
    public string? WinnerId { get; set; }
}

public class Player
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool HasFinished { get; set; } = false;
    public int Score { get; set; } = 0; // Best score achieved
    public int BestRound { get; set; } = 0; // Round where best score was achieved
    public int GuessCount { get; set; } = 0;
    public List<string> Guesses { get; set; } = new();
    public List<string[]> GuessFeedback { get; set; } = new(); // Store feedback for each guess
    
    // Turn-based properties
    public string? PendingGuess { get; set; } = null; // Current round guess waiting for opponent
    public bool HasSubmittedCurrentRound { get; set; } = false; // Has submitted for current round
}

public class MultiplayerGuessRequest
{
    public string GameId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Guess { get; set; } = string.Empty;
}

public class MultiplayerGuessResponse
{
    public string[] Feedback { get; set; } = Array.Empty<string>();
    public bool IsCorrect { get; set; }
    public bool IsGameCompleted { get; set; }
    public bool IsWon { get; set; }
    public int RemainingTrials { get; set; }
    public int Score { get; set; }
    public string? WinnerId { get; set; }
    
    // Turn-based properties
    public bool IsWaitingForOpponent { get; set; } = false; // True if waiting for opponent to submit
    public string? OpponentGuess { get; set; } = null; // Opponent's guess (if both submitted)
    public string[] OpponentFeedback { get; set; } = Array.Empty<string>(); // Opponent's feedback
}

// For opponent updates (only colors, no letters)
public class OpponentUpdate
{
    public string PlayerName { get; set; } = string.Empty;
    public int GuessNumber { get; set; }
    public string[] FeedbackColors { get; set; } = Array.Empty<string>();
    public int Score { get; set; }
    public int BestRound { get; set; }
    public bool HasFinished { get; set; }
}
