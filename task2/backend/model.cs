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
    
    // Cheating mode properties
    public bool IsCheatingMode { get; set; } = false;
    public List<string> RemainingCandidates { get; set; } = new();
    public List<string> AllWords { get; set; } = new();

    // Calculate score based on feedback: hit = 2 points, present = 1 point, miss = 0 points
    // Returns the highest score from any single guess
    public int CalculateScore()
    {
        int maxScore = 0;
        
        foreach (var guess in Guesses)
        {
            int guessScore = 0;
            foreach (var feedback in guess.Feedback)
            {
                switch (feedback.ToLower())
                {
                    case "correct":
                    case "hit":
                        guessScore += 2;
                        break;
                    case "present":
                        guessScore += 1;
                        break;
                    case "miss":
                    default:
                        guessScore += 0;
                        break;
                }
            }
            maxScore = Math.Max(maxScore, guessScore);
        }
        
        return maxScore;
    }
}

public class GuessResult
{
    public string Guess { get; set; } = string.Empty;
    public string[] Feedback { get; set; } = Array.Empty<string>();
    public DateTime GuessedAt { get; set; }
}

// Request/Response DTOs
public class StartGameRequest
{
    public bool IsCheatingMode { get; set; } = true;
}

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
    public int Score { get; set; } = 0;
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
    public bool IsGameCompleted { get; set; }
    public bool IsWon { get; set; }
    public int RemainingTrials { get; set; }
    public List<char> UsedCharacters { get; set; } = new();
    public int Score { get; set; } = 0;
}