using Microsoft.Extensions.Caching.Memory;

public interface IGameSessionService
{
    Task<StartGameResponse> StartNewGameAsync();
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

    public async Task<StartGameResponse> StartNewGameAsync()
    {
        var sessionId = Guid.NewGuid().ToString();
        var words = _configuration["WORDS"]?.Split(',').Select(w => w.Trim()).ToArray() ?? new[] { "apple", "girls", "sunny" };
        var answer = WordleGameLogic.SelectRandomWord(words);
        var maxTrials = int.Parse(_configuration["MAXIMUM_TRIALS"] ?? "3");
        var sessionTimeoutMinutes = int.Parse(_configuration["SESSION_TIMEOUT_MINUTES"] ?? "30");
        var expiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);

        var session = new GameSession
        {
            SessionId = sessionId,
            Answer = answer.ToLower(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            MaxTrials = maxTrials,
            IsCompleted = false,
            IsWon = false
        };

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt,
            SlidingExpiration = TimeSpan.FromMinutes(sessionTimeoutMinutes)
        };

        _cache.Set(sessionId, session, cacheOptions);

        _logger.LogInformation("New game session created: {SessionId}", sessionId);

        return new StartGameResponse
        {
            SessionId = sessionId,
            MaxTrials = maxTrials,
            ExpiresAt = expiresAt
        };
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
                ExpiresAt = session.ExpiresAt
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

    public async Task<SubmitGuessResponse> SubmitGuessAsync(SubmitGuessRequest request)
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

        // Evaluate the guess
        var feedback = WordleGameLogic.EvaluateGuess(guess, session.Answer);
        var isCorrect = guess == session.Answer;

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

        return new SubmitGuessResponse
        {
            Feedback = feedback,
            IsCorrect = isCorrect,
            GameOver = session.IsCompleted,
            RemainingTrials = session.MaxTrials - session.Guesses.Count,
            Answer = session.IsCompleted ? session.Answer.ToUpper() : null
        };
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        //memory cache default cleanup is handled by the framework
        _logger.LogInformation("Cleanup task executed at {Time}", DateTime.UtcNow);
        await Task.CompletedTask;
    }
}
