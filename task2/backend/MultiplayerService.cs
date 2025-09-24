using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.SignalR;
using System.Collections;

public interface IMultiplayerService
{
    Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request);
    Task<GameState?> GetGameStateAsync(string gameId);
    Task<MultiplayerGuessResponse> SubmitGuessAsync(MultiplayerGuessRequest request);
    Task UpdatePlayerConnectionAsync(string gameId, string playerId, string connectionId);
}

public class MultiplayerService : IMultiplayerService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MultiplayerService> _logger;
    private readonly IHubContext<MultiplayerHub> _hubContext;
    
    // Simple queue for waiting players - store both player ID and game ID
    private static readonly List<(string PlayerId, string GameId)> _waitingPlayers = new();
    private static readonly object _lock = new object();

    public MultiplayerService(
        IMemoryCache cache, 
        IConfiguration configuration, 
        ILogger<MultiplayerService> logger,
        IHubContext<MultiplayerHub> hubContext)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        _hubContext = hubContext;
    }

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request)
    {
        var playerId = Guid.NewGuid().ToString();
        
        lock (_lock)
        {
            // Check if there's a waiting player
            if (_waitingPlayers.Count > 0)
            {
                // Match with waiting player
                var (waitingPlayerId, waitingGameId) = _waitingPlayers[0];
                _waitingPlayers.RemoveAt(0);
                
                // Get the waiting player's game
                if (_cache.TryGetValue(waitingGameId, out GameState? game) && game != null)
                {
                    _logger.LogInformation("Found waiting game {GameId} for player {PlayerId}", waitingGameId, waitingPlayerId);
                    // Add second player to existing game
                    var newPlayer = new Player
                    {
                        PlayerId = playerId,
                        PlayerName = request.PlayerName
                    };
                    game.Players.Add(newPlayer);
                    game.Status = "InProgress"; // Start immediately when 2 players join
                    
                    // Update cache
                    var updateTimeoutMinutes = int.Parse(_configuration["SESSION_TIMEOUT_MINUTES"] ?? "30");
                    var updateCacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTime.UtcNow.AddMinutes(updateTimeoutMinutes),
                        SlidingExpiration = TimeSpan.FromMinutes(updateTimeoutMinutes)
                    };
                    _cache.Set(waitingGameId, game, updateCacheOptions);
                    
                    _logger.LogInformation("Player {PlayerName} joined game {GameId}, game started", 
                        request.PlayerName, waitingGameId);
                    
                    // Create sanitized game state without answer
                    var sanitizedGame = new GameState
                    {
                        GameId = game.GameId,
                        Players = game.Players,
                        Status = game.Status,
                        MaxTrials = game.MaxTrials,
                        CreatedAt = game.CreatedAt,
                        WinnerId = game.WinnerId,
                        Answer = string.Empty // Never send answer to client
                    };
                    
                    return Task.FromResult(new JoinGameResponse
                    {
                        GameId = waitingGameId,
                        PlayerId = playerId,
                        PlayerName = request.PlayerName,
                        IsWaiting = false,
                        GameState = sanitizedGame
                    });
                }
                else
                {
                    _logger.LogWarning("Could not find waiting game {GameId} for player {PlayerId}, creating new game instead", waitingGameId, waitingPlayerId);
                    // Don't put the waiting player back since their game is gone
                }
            }
            
            // No waiting player, create new game and wait
            var gameId = Guid.NewGuid().ToString();
            var sessionTimeoutMinutes = int.Parse(_configuration["SESSION_TIMEOUT_MINUTES"] ?? "30");
            var maxTrials = int.Parse(_configuration["MAXIMUM_TRIALS"] ?? "6");
            // Start with empty answer for cheating mode - will be selected dynamically
            var selectedWord = string.Empty;

            var newGame = new GameState
            {
                GameId = gameId,
                Answer = selectedWord,
                MaxTrials = maxTrials,
                Status = "Waiting",
                CreatedAt = DateTime.UtcNow,
                Players = new List<Player>
                {
                    new Player
                    {
                        PlayerId = playerId,
                        PlayerName = request.PlayerName
                    }
                }
            };

            // Cache the game
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(sessionTimeoutMinutes)
            };
            _cache.Set(gameId, newGame, cacheOptions);
            
            // Add to waiting queue with game ID
            _waitingPlayers.Add((playerId, gameId));
            
            _logger.LogInformation("Player {PlayerName} created new game {GameId} and is waiting", 
                request.PlayerName, gameId);

            // Create sanitized game state without answer
            var sanitizedNewGame = new GameState
            {
                GameId = newGame.GameId,
                Players = newGame.Players,
                Status = newGame.Status,
                MaxTrials = newGame.MaxTrials,
                CreatedAt = newGame.CreatedAt,
                WinnerId = newGame.WinnerId,
                Answer = string.Empty // Never send answer to client
            };

            return Task.FromResult(new JoinGameResponse
            {
                GameId = gameId,
                PlayerId = playerId,
                PlayerName = request.PlayerName,
                IsWaiting = true,
                GameState = sanitizedNewGame
            });
        }
    }

    public Task<GameState?> GetGameStateAsync(string gameId)
    {
        if (_cache.TryGetValue(gameId, out GameState? game))
        {
            // Create a sanitized version without the answer
            var sanitizedGame = new GameState
            {
                GameId = game.GameId,
                Players = game.Players,
                Status = game.Status,
                MaxTrials = game.MaxTrials,
                CreatedAt = game.CreatedAt,
                WinnerId = game.WinnerId,
                Answer = string.Empty // Never send the answer to client
            };
            return Task.FromResult<GameState?>(sanitizedGame);
        }
        return Task.FromResult<GameState?>(null);
    }

    public async Task<MultiplayerGuessResponse> SubmitGuessAsync(MultiplayerGuessRequest request)
    {
        _logger.LogInformation("Processing guess '{Guess}' from player {PlayerId} in game {GameId}", 
            request.Guess, request.PlayerId, request.GameId);

        if (!_cache.TryGetValue(request.GameId, out GameState? game) || game == null)
        {
            throw new InvalidOperationException("Game not found");
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);
        if (player == null)
        {
            throw new InvalidOperationException("Player not found");
        }

        if (game.Status != "InProgress")
        {
            throw new InvalidOperationException("Game is not in progress");
        }

        // Validate guess
        if (string.IsNullOrEmpty(request.Guess) || request.Guess.Length != 5)
        {
            throw new ArgumentException("Guess must be exactly 5 letters");
        }

        if (!WordleGameLogic.IsValidWord(request.Guess))
        {
            throw new ArgumentException("Guess must contain only alphabetic characters");
        }

        // Store the pending guess for this player
        player.PendingGuess = request.Guess.ToLower();
        player.HasSubmittedCurrentRound = true;
        
        _logger.LogInformation("Player {PlayerId} submitted guess '{Guess}' for current round in game {GameId}", 
            request.PlayerId, request.Guess, game.GameId);
        
        // Check if both players have submitted for this round
        var opponent = game.Players.FirstOrDefault(p => p.PlayerId != request.PlayerId);
        if (opponent == null)
        {
            throw new InvalidOperationException("Opponent not found");
        }
        
        // If opponent hasn't submitted yet, wait
        if (!opponent.HasSubmittedCurrentRound || string.IsNullOrEmpty(opponent.PendingGuess))
        {
            _logger.LogInformation("Waiting for opponent {OpponentId} to submit guess in game {GameId}", 
                opponent.PlayerId, game.GameId);
            
            // Update cache and return waiting response
            _cache.Set(request.GameId, game);
            
            return new MultiplayerGuessResponse
            {
                Feedback = Array.Empty<string>(),
                IsCorrect = false,
                IsGameCompleted = false,
                IsWon = false,
                RemainingTrials = game.MaxTrials - player.GuessCount,
                Score = player.Score,
                WinnerId = game.WinnerId,
                IsWaitingForOpponent = true
            };
        }
        
        // Both players have submitted - process the round with cheating logic
        _logger.LogInformation("Both players submitted - processing round with cheating logic in game {GameId}", game.GameId);
        
        return await ProcessBothGuesses(game, player, opponent, request.GameId);
    }

    public Task UpdatePlayerConnectionAsync(string gameId, string playerId, string connectionId)
    {
        if (_cache.TryGetValue(gameId, out GameState? game) && game != null)
        {
            var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.ConnectionId = connectionId;
                _cache.Set(gameId, game);
                
                // If this is the second player joining, notify about game start
                if (game.Players.Count == 2 && game.Status == "InProgress")
                {
                    _hubContext.Clients.Group(gameId).SendAsync("GameStarted", game);
                }
                
                _logger.LogInformation("Player {PlayerId} connected to SignalR in game {GameId}", playerId, gameId);
            }
        }
        return Task.CompletedTask;
    }


    private int CalculateRoundScore(string[] feedback)
    {
        // Each hit = 2 points, each present = 1 point, miss = 0 points
        int score = 0;
        foreach (var result in feedback)
        {
            if (result == "correct") score += 2;
            else if (result == "present") score += 1;
            // absent = 0 points
        }
        return score;
    }

    private (int bestScore, int roundAchieved) CalculatePlayerBestScore(Player player)
    {
        int bestScore = 0;
        int bestRound = 0;
        
        for (int i = 0; i < player.GuessFeedback.Count; i++)
        {
            int roundScore = CalculateRoundScore(player.GuessFeedback[i]);
            if (roundScore > bestScore)
            {
                bestScore = roundScore;
                bestRound = i + 1; // 1-based round number
            }
        }
        
        return (bestScore, bestRound);
    }

    private void DetermineWinner(GameState game)
    {
        if (game.Players.Count != 2) return;
        
        var player1 = game.Players[0];
        var player2 = game.Players[1];
        
        _logger.LogInformation("Determining winner: Player1 {Name} - Score: {Score} (Round {Round}), Player2 {Name} - Score: {Score2} (Round {Round2})",
            player1.PlayerName, player1.Score, player1.BestRound,
            player2.PlayerName, player2.Score, player2.BestRound);
        
        // Winner determination rules:
        // 1. Player who got the correct answer wins (if any)
        // 2. If no correct answer, higher score wins
        // 3. If same score, earlier round wins
        // 4. If same score and same round, it's a tie (first player wins by default)
        
        // Check if either player got the correct answer
        var player1HasCorrectAnswer = player1.Guesses.Any(guess => 
            WordleGameLogic.EvaluateGuess(guess, game.Answer).All(f => f == "correct"));
        var player2HasCorrectAnswer = player2.Guesses.Any(guess => 
            WordleGameLogic.EvaluateGuess(guess, game.Answer).All(f => f == "correct"));
        
        if (player1HasCorrectAnswer && !player2HasCorrectAnswer)
        {
            game.WinnerId = player1.PlayerId;
            _logger.LogInformation("Player1 {Name} wins with correct answer!", player1.PlayerName);
        }
        else if (player2HasCorrectAnswer && !player1HasCorrectAnswer)
        {
            game.WinnerId = player2.PlayerId;
            _logger.LogInformation("Player2 {Name} wins with correct answer!", player2.PlayerName);
        }
        else if (player1HasCorrectAnswer && player2HasCorrectAnswer)
        {
            // Both got correct answer - winner is who got it first (fewer guesses)
            var player1CorrectRound = player1.Guesses.FindIndex(guess => 
                WordleGameLogic.EvaluateGuess(guess, game.Answer).All(f => f == "correct")) + 1;
            var player2CorrectRound = player2.Guesses.FindIndex(guess => 
                WordleGameLogic.EvaluateGuess(guess, game.Answer).All(f => f == "correct")) + 1;
            
            if (player1CorrectRound < player2CorrectRound)
            {
                game.WinnerId = player1.PlayerId;
                _logger.LogInformation("Player1 {Name} wins - got correct answer in round {Round} vs {Round2}!", 
                    player1.PlayerName, player1CorrectRound, player2CorrectRound);
            }
            else if (player2CorrectRound < player1CorrectRound)
            {
                game.WinnerId = player2.PlayerId;
                _logger.LogInformation("Player2 {Name} wins - got correct answer in round {Round} vs {Round2}!", 
                    player2.PlayerName, player2CorrectRound, player1CorrectRound);
            }
            else
            {
                // Same round - it's a tie!
                game.WinnerId = null;
                _logger.LogInformation("Tie - both players got correct answer in round {Round}!", player1CorrectRound);
            }
        }
        else
        {
            // Neither got correct answer - use score-based logic
            if (player1.Score > player2.Score)
            {
                game.WinnerId = player1.PlayerId;
            }
            else if (player2.Score > player1.Score)
            {
                game.WinnerId = player2.PlayerId;
            }
            else if (player1.Score == player2.Score)
            {
                // Same score - check who achieved it first (lower round number wins)
                if (player1.BestRound < player2.BestRound)
                {
                    game.WinnerId = player1.PlayerId;
                }
                else if (player2.BestRound < player1.BestRound)
                {
                    game.WinnerId = player2.PlayerId;
                }
                else
                {
                    // Same score, same round - it's a tie!
                    game.WinnerId = null;
                    _logger.LogInformation("Tie - both players achieved same score {Score} in round {Round}!", 
                        player1.Score, player1.BestRound);
                }
            }
        }
        
        _logger.LogInformation("Winner determined: {WinnerId}", game.WinnerId);
    }

    private async Task<MultiplayerGuessResponse> ProcessBothGuesses(GameState game, Player currentPlayer, Player opponent, string gameId)
    {
        // Get both pending guesses
        var currentGuess = currentPlayer.PendingGuess!;
        var opponentGuess = opponent.PendingGuess!;
        
        _logger.LogInformation("Processing both guesses - Player1: '{Guess1}', Player2: '{Guess2}' in game {GameId}", 
            currentGuess, opponentGuess, gameId);
        
        // Use cheating logic to select optimal answer considering both guesses
        string selectedAnswer;
        string[] currentFeedback;
        string[] opponentFeedback;
        
        if (string.IsNullOrEmpty(game.Answer))
        {
            // First round - use cheating logic with both guesses
            var wordsConfig = _configuration["WORDS"] ?? "apple,girls,sunny";
            var words = wordsConfig.Split(',').Select(w => w.Trim().ToLower()).ToArray();
            
            // Get previous guesses for both players
            var currentPlayerHistory = currentPlayer.Guesses.Zip(currentPlayer.GuessFeedback, 
                (g, f) => new GuessResult { Guess = g, Feedback = f }).ToList();
            var opponentHistory = opponent.Guesses.Zip(opponent.GuessFeedback, 
                (g, f) => new GuessResult { Guess = g, Feedback = f }).ToList();
            
            // Select answer that makes both guesses as difficult as possible
            selectedAnswer = SelectOptimalAnswerForBothGuesses(words.ToList(), currentGuess, opponentGuess, 
                currentPlayerHistory, opponentHistory);
            
            game.Answer = selectedAnswer;
            _logger.LogInformation("Cheating logic selected answer '{Answer}' for both guesses in game {GameId}", 
                selectedAnswer, gameId);
        }
        else
        {
            // Answer already set from previous round
            selectedAnswer = game.Answer;
            _logger.LogInformation("Using existing answer '{Answer}' for both guesses in game {GameId}", 
                selectedAnswer, gameId);
        }
        
        // Evaluate both guesses against the selected answer
        currentFeedback = WordleGameLogic.EvaluateGuess(currentGuess, selectedAnswer);
        opponentFeedback = WordleGameLogic.EvaluateGuess(opponentGuess, selectedAnswer);
        
        // Update both players' game state
        UpdatePlayerAfterRound(currentPlayer, currentGuess, currentFeedback);
        UpdatePlayerAfterRound(opponent, opponentGuess, opponentFeedback);
        
        // Reset round submission flags
        currentPlayer.HasSubmittedCurrentRound = false;
        opponent.HasSubmittedCurrentRound = false;
        currentPlayer.PendingGuess = null;
        opponent.PendingGuess = null;
        
        // Check if either player completed the game
        var currentIsCorrect = currentFeedback.All(f => f == "correct");
        var opponentIsCorrect = opponentFeedback.All(f => f == "correct");
        
        // If ANY player gets the correct answer, BOTH players finish the game
        var anyPlayerWon = currentIsCorrect || opponentIsCorrect;
        var maxTrialsReached = currentPlayer.GuessCount >= game.MaxTrials || opponent.GuessCount >= game.MaxTrials;
        
        if (anyPlayerWon || maxTrialsReached)
        {
            currentPlayer.HasFinished = true;
            opponent.HasFinished = true;
            _logger.LogInformation("Game ending - Any player won: {AnyWon}, Max trials: {MaxTrials}", 
                anyPlayerWon, maxTrialsReached);
        }
        
        // Check if game is complete
        var gameComplete = game.Players.All(p => p.HasFinished);
        if (gameComplete)
        {
            game.Status = "Completed";
            DetermineWinner(game);
        }
        
        // Update cache
        _cache.Set(gameId, game);
        
        // Notify both players simultaneously
        await NotifyBothPlayersAfterRound(game, currentPlayer, opponent, currentFeedback, opponentFeedback);
        
        // Send round completion to both players with their individual feedback
        await NotifyRoundCompletion(game, currentPlayer, opponent, currentFeedback, opponentFeedback);
        
        // Return response for current player
        return new MultiplayerGuessResponse
        {
            Feedback = currentFeedback,
            IsCorrect = currentIsCorrect,
            IsGameCompleted = currentPlayer.HasFinished,
            IsWon = currentIsCorrect,
            RemainingTrials = game.MaxTrials - currentPlayer.GuessCount,
            Score = currentPlayer.Score,
            WinnerId = game.WinnerId,
            IsWaitingForOpponent = false,
            OpponentGuess = opponentGuess.ToUpper(),
            OpponentFeedback = opponentFeedback
        };
    }

    private void UpdatePlayerAfterRound(Player player, string guess, string[] feedback)
    {
        player.Guesses.Add(guess.ToUpper());
        player.GuessFeedback.Add(feedback);
        player.GuessCount++;
        
        // Calculate round score and update player's best score
        int roundScore = CalculateRoundScore(feedback);
        if (roundScore > player.Score)
        {
            player.Score = roundScore;
            player.BestRound = player.GuessCount;
        }
        
        _logger.LogInformation("Player {PlayerId} round updated: {Guess} -> [{Feedback}], Round Score: {RoundScore}, Best Score: {BestScore} (Round {BestRound})", 
            player.PlayerId, guess.ToUpper(), string.Join(",", feedback), roundScore, player.Score, player.BestRound);
    }

    private string SelectOptimalAnswerForBothGuesses(List<string> words, string guess1, string guess2, 
        List<GuessResult> history1, List<GuessResult> history2)
    {
        // Filter candidates that match both players' histories
        var validCandidates = words.Where(candidate =>
        {
            // Check if candidate matches player 1's history
            foreach (var prevGuess in history1)
            {
                var expectedFeedback = WordleGameLogic.EvaluateGuess(prevGuess.Guess, candidate);
                if (!expectedFeedback.SequenceEqual(prevGuess.Feedback))
                    return false;
            }
            
            // Check if candidate matches player 2's history
            foreach (var prevGuess in history2)
            {
                var expectedFeedback = WordleGameLogic.EvaluateGuess(prevGuess.Guess, candidate);
                if (!expectedFeedback.SequenceEqual(prevGuess.Feedback))
                    return false;
            }
            
            return true;
        }).ToList();
        
        if (!validCandidates.Any())
        {
            _logger.LogWarning("No valid candidates found - using first word from list");
            return words.First();
        }
        
        // Score each candidate against both current guesses
        var candidateScores = validCandidates.Select(candidate =>
        {
            var score1 = CheatingWordleLogic.EvaluateGuessScore(guess1, candidate);
            var score2 = CheatingWordleLogic.EvaluateGuessScore(guess2, candidate);
            
            return new
            {
                Word = candidate,
                TotalHits = score1.Hits + score2.Hits,
                TotalPresents = score1.Presents + score2.Presents,
                TotalScore = score1.TotalScore + score2.TotalScore
            };
        }).ToList();
        
        // Select candidate with lowest total score (hardest for both players)
        var selectedCandidate = candidateScores
            .OrderBy(c => c.TotalHits)
            .ThenBy(c => c.TotalPresents)
            .First();
        
        _logger.LogInformation("Selected answer '{Answer}' with {Hits} total hits, {Presents} total presents for both guesses", 
            selectedCandidate.Word, selectedCandidate.TotalHits, selectedCandidate.TotalPresents);
        
        return selectedCandidate.Word;
    }

    private async Task NotifyBothPlayersAfterRound(GameState game, Player player1, Player player2, 
        string[] feedback1, string[] feedback2)
    {
        // Notify player 1 about player 2's progress
        if (!string.IsNullOrEmpty(player1.ConnectionId))
        {
            var update1 = new OpponentUpdate
            {
                PlayerName = player2.PlayerName,
                GuessNumber = player2.GuessCount,
                FeedbackColors = feedback2,
                Score = player2.Score,
                BestRound = player2.BestRound,
                HasFinished = player2.HasFinished
            };
            await _hubContext.Clients.Client(player1.ConnectionId).SendAsync("OpponentGuessUpdate", update1);
        }
        
        // Notify player 2 about player 1's progress
        if (!string.IsNullOrEmpty(player2.ConnectionId))
        {
            var update2 = new OpponentUpdate
            {
                PlayerName = player1.PlayerName,
                GuessNumber = player1.GuessCount,
                FeedbackColors = feedback1,
                Score = player1.Score,
                BestRound = player1.BestRound,
                HasFinished = player1.HasFinished
            };
            await _hubContext.Clients.Client(player2.ConnectionId).SendAsync("OpponentGuessUpdate", update2);
        }
        
        // If game is complete, notify all players
        if (game.Status == "Completed")
        {
            await _hubContext.Clients.Group(game.GameId).SendAsync("GameCompleted", new
            {
                GameId = game.GameId,
                WinnerId = game.WinnerId,
                Players = game.Players.Select(p => new { 
                    p.PlayerId,
                    p.PlayerName, 
                    p.Score, 
                    p.BestRound,
                    p.HasFinished,
                    p.GuessCount
                })
            });
        }
    }

    private async Task NotifyRoundCompletion(GameState game, Player player1, Player player2, 
        string[] feedback1, string[] feedback2)
    {
        // Send round completion to player 1 with their feedback
        if (!string.IsNullOrEmpty(player1.ConnectionId))
        {
            await _hubContext.Clients.Client(player1.ConnectionId).SendAsync("RoundCompleted", new
            {
                MyFeedback = feedback1,
                MyGuess = player1.Guesses.Last().ToUpper(),
                OpponentGuess = player2.Guesses.Last().ToUpper(),
                OpponentFeedback = feedback2,
                MyScore = player1.Score,
                OpponentScore = player2.Score,
                IsGameCompleted = player1.HasFinished,
                RemainingTrials = game.MaxTrials - player1.GuessCount
            });
        }
        
        // Send round completion to player 2 with their feedback
        if (!string.IsNullOrEmpty(player2.ConnectionId))
        {
            await _hubContext.Clients.Client(player2.ConnectionId).SendAsync("RoundCompleted", new
            {
                MyFeedback = feedback2,
                MyGuess = player2.Guesses.Last().ToUpper(),
                OpponentGuess = player1.Guesses.Last().ToUpper(),
                OpponentFeedback = feedback1,
                MyScore = player2.Score,
                OpponentScore = player1.Score,
                IsGameCompleted = player2.HasFinished,
                RemainingTrials = game.MaxTrials - player2.GuessCount
            });
        }
    }
}
