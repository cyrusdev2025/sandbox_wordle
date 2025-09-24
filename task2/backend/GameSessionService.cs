using Microsoft.Extensions.Caching.Memory;

public interface IGameSessionService
{
    Task<StartGameResponse> StartGameAsync(StartGameRequest? request = null);
    Task<ValidateSessionResponse> ValidateSessionAsync(string sessionId);
    Task<SubmitGuessResponse> SubmitGuessAsync(SubmitGuessRequest request);
    Task CleanupExpiredSessionsAsync();
}

public class GameSessionService : IGameSessionService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GameSessionService> _logger;

    public GameSessionService(IMemoryCache cache, IConfiguration configuration, ILogger<GameSessionService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<StartGameResponse> StartGameAsync(StartGameRequest? request = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var sessionTimeoutMinutes = int.Parse(_configuration["SESSION_TIMEOUT_MINUTES"] ?? "30");
        var maxTrials = int.Parse(_configuration["MAXIMUM_TRIALS"] ?? "6");
        var wordsConfig = _configuration["WORDS"] ?? "apple,girls,sunny";
        var words = wordsConfig.Split(',').Select(w => w.Trim().ToLower()).ToArray();
        
        var isCheatingMode = request?.IsCheatingMode ?? true;
        var selectedWord = isCheatingMode ? string.Empty : WordleGameLogic.SelectRandomWord(words);
        
        var session = new GameSession
        {
            SessionId = sessionId,
            Answer = selectedWord,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes),
            MaxTrials = maxTrials,
            IsCheatingMode = isCheatingMode,
            AllWords = isCheatingMode ? words.ToList() : new List<string>(),
            RemainingCandidates = isCheatingMode ? words.ToList() : new List<string>(),
            IsCompleted = false,
            IsWon = false
        };

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = session.ExpiresAt,
            SlidingExpiration = TimeSpan.FromMinutes(sessionTimeoutMinutes)
        };

        _cache.Set(sessionId, session, cacheOptions);

        _logger.LogInformation("New game session created: {SessionId}", sessionId);

        return Task.FromResult(new StartGameResponse
        {
            SessionId = sessionId,
            MaxTrials = maxTrials,
            ExpiresAt = session.ExpiresAt
        });
    }

    public async Task<ValidateSessionResponse> ValidateSessionAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return new ValidateSessionResponse
            {
                SessionId = sessionId,
                IsValid = false,
                ErrorMessage = "Session ID is required"
            };
        }

        // Try to get existing session
        if (_cache.TryGetValue(sessionId, out GameSession? session) && session != null)
        {
            // Check if session is already completed
            if (session.IsCompleted)
            {
                _logger.LogInformation("Session validation failed - session already completed: {SessionId}", sessionId);
                return new ValidateSessionResponse
                {
                    SessionId = sessionId,
                    IsValid = false,
                    ErrorMessage = "Game session has already been completed"
                };
            }

            // Session exists and is valid
            _logger.LogInformation("Session validation successful: {SessionId}", sessionId);
            
            return new ValidateSessionResponse
            {
                SessionId = session.SessionId,
                IsValid = true,
                MaxTrials = session.MaxTrials,
                RemainingTrials = session.MaxTrials - session.Guesses.Count,
                IsCompleted = session.IsCompleted,
                IsWon = session.IsWon,
                TriedGuesses = session.Guesses.Select(g => g.Guess).ToList(),
                GuessFeedback = session.Guesses.Select(g => g.Feedback).ToList(),
                UsedCharacters = session.UsedCharacters.ToList(),
                ExpiresAt = session.ExpiresAt,
                Score = session.CalculateScore()
            };
        }

        // Session not found or expired
        _logger.LogWarning("Session validation failed: {SessionId} not found or expired", sessionId);
        return new ValidateSessionResponse
        {
            SessionId = sessionId,
            IsValid = false,
            ErrorMessage = "Session not found or expired"
        };
    }

    public Task<SubmitGuessResponse> SubmitGuessAsync(SubmitGuessRequest request)
    {
        if (!_cache.TryGetValue(request.SessionId, out GameSession? session) || session == null)
        {
            throw new InvalidOperationException("Session not found or expired");
        }

        if (session.IsCompleted)
        {
            throw new InvalidOperationException("Game session is already completed");
        }

        var guess = request.Guess.ToLower().Trim();

        // Input validation
        if (guess.Length != 5)
        {
            throw new ArgumentException("Guess must be exactly 5 letters");
        }

        if (!WordleGameLogic.IsValidWord(guess))
        {
            throw new ArgumentException("Guess must contain only alphabetic characters");
        }

        string[] feedback;
        bool isCorrect;
        
        if (session.IsCheatingMode)
        {
            // Use cheating logic
            var cheatingResult = CheatingWordleLogic.ProcessCheatingGuess(
                guess, 
                session.AllWords, 
                session.Guesses);
            
            // Update session with selected answer
            session.Answer = cheatingResult.SelectedAnswer;
            session.RemainingCandidates = cheatingResult.RemainingCandidates;
            
            feedback = cheatingResult.Feedback;
            isCorrect = guess.Equals(session.Answer, StringComparison.OrdinalIgnoreCase);
            
            _logger.LogInformation("Cheating mode - Selected answer: {Answer}, Reason: {Reason}, Remaining: {Count}", 
                session.Answer, cheatingResult.SelectionReason, session.RemainingCandidates.Count);
        }
        else
        {
            // Normal mode
            feedback = WordleGameLogic.EvaluateGuess(guess, session.Answer);
            isCorrect = guess.Equals(session.Answer, StringComparison.OrdinalIgnoreCase);
        }

        var guessResult = new GuessResult
        {
            Guess = guess,
            Feedback = feedback,
            GuessedAt = DateTime.UtcNow
        };

        session.Guesses.Add(guessResult);
        
        // Update used characters
        foreach (char c in guess)
        {
            session.UsedCharacters.Add(c);
        }

        // Check win condition
        if (isCorrect)
        {
            session.IsCompleted = true;
            session.IsWon = true;
        }
        // Check lose condition
        else if (session.Guesses.Count >= session.MaxTrials)
        {
            session.IsCompleted = true;
            session.IsWon = false;
        }

        // Update session in cache
        var sessionTimeoutMinutes = int.Parse(_configuration["SESSION_TIMEOUT_MINUTES"] ?? "30");
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = session.ExpiresAt,
            SlidingExpiration = TimeSpan.FromMinutes(sessionTimeoutMinutes)
        };
        _cache.Set(request.SessionId, session, cacheOptions);

        _logger.LogInformation("Guess submitted for session {SessionId}: {Guess} -> {IsCorrect}", 
            request.SessionId, guess, isCorrect);

        return Task.FromResult(new SubmitGuessResponse
        {
            Feedback = feedback,
            IsCorrect = isCorrect,
            IsGameCompleted = session.IsCompleted,
            IsWon = session.IsWon,
            RemainingTrials = session.MaxTrials - session.Guesses.Count,
            UsedCharacters = session.UsedCharacters.ToList(),
            Score = session.CalculateScore()
        });
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        //memory cache default cleanup is handled by the framework
        _logger.LogInformation("Cleanup task executed at {Time}", DateTime.UtcNow);
        await Task.CompletedTask;
    }
}
