// Models
public class GameSession
{
    public string SessionId { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<GuessResult> Guesses { get; set; } = new();
    public HashSet<char> UsedCharacters { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsWon { get; set; }
    public int MaxTrials { get; set; }
}

public class GuessResult
{
    public string Guess { get; set; } = string.Empty;
    public string[] Feedback { get; set; } = Array.Empty<string>();
    public DateTime GuessedAt { get; set; }
}

// Request/Response DTOs
public class StartGameResponse
{
    public string SessionId { get; set; } = string.Empty;
    public int MaxTrials { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ValidateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int MaxTrials { get; set; }
    public int RemainingTrials { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsWon { get; set; }
    public List<string> TriedGuesses { get; set; } = new();
    public List<string[]> GuessFeedback { get; set; } = new();
    public List<char> UsedCharacters { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

public class SubmitGuessRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Guess { get; set; } = string.Empty;
}

public class SubmitGuessResponse
{
    public string[] Feedback { get; set; } = Array.Empty<string>();
    public bool IsCorrect { get; set; }
    public bool GameOver { get; set; }
    public int RemainingTrials { get; set; }
    public string? Answer { get; set; } // Only revealed when game over
}