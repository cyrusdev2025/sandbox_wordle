using System.Linq;

public static class CheatingWordleLogic
{
    public class GuessScore
    {
        public string Word { get; set; } = string.Empty;
        public int Hits { get; set; }
        public int Presents { get; set; }
        public int TotalScore => Hits * 10 + Presents; // Hit priority over Present
    }

    /// <summary>
    /// Evaluates a guess against a candidate word and returns the score
    /// </summary>
    public static GuessScore EvaluateGuessScore(string guess, string candidate)
    {
        var feedback = WordleGameLogic.EvaluateGuess(guess, candidate);
        
        int hits = feedback.Count(f => f == "correct");
        int presents = feedback.Count(f => f == "present");
        
        return new GuessScore
        {
            Word = candidate,
            Hits = hits,
            Presents = presents
        };
    }

    /// <summary>
    /// Filters candidates that match all previous guess results
    /// </summary>
    public static List<string> FilterMatchingCandidates(
        List<string> candidates, 
        List<GuessResult> previousGuesses)
    {
        var validCandidates = new List<string>();

        foreach (var candidate in candidates)
        {
            bool isValid = true;

            // Check if this candidate would produce the same feedback for all previous guesses
            foreach (var previousGuess in previousGuesses)
            {
                var expectedFeedback = WordleGameLogic.EvaluateGuess(previousGuess.Guess, candidate);
                
                // Compare feedback arrays
                if (!expectedFeedback.SequenceEqual(previousGuess.Feedback))
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
            {
                validCandidates.Add(candidate);
            }
        }

        return validCandidates;
    }

    /// <summary>
    /// Selects the best answer from candidates to maximize difficulty
    /// </summary>
    public static CheatingWordSelection SelectOptimalAnswer(
        string currentGuess, 
        List<string> candidates)
    {
        if (!candidates.Any())
        {
            // Return perfect score (10) when no valid candidates remain
            return new CheatingWordSelection
            {
                SelectedAnswer = currentGuess,
                Feedback = new string[] { "correct", "correct", "correct", "correct", "correct" },
                RemainingCandidates = new List<string>(),
                SelectionReason = "No valid candidates remaining - returning perfect score"
            };
        }

        // If only one candidate, use it
        if (candidates.Count == 1)
        {
            var singleCandidate = candidates[0];
            var singleScore = EvaluateGuessScore(currentGuess, singleCandidate);
            
            return new CheatingWordSelection
            {
                SelectedAnswer = singleCandidate,
                Feedback = WordleGameLogic.EvaluateGuess(currentGuess, singleCandidate),
                RemainingCandidates = new List<string> { singleCandidate },
                SelectionReason = "Only one candidate remaining"
            };
        }

        // Evaluate all candidates against current guess
        var candidateScores = candidates
            .Select(candidate => EvaluateGuessScore(currentGuess, candidate))
            .ToList();

        // Group by score (hits first, then presents)
        var scoreGroups = candidateScores
            .GroupBy(score => new { score.Hits, score.Presents })
            .OrderBy(g => g.Key.Hits)           // Fewer hits first (harder)
            .ThenBy(g => g.Key.Presents)        // Then fewer presents (harder)
            .ToList();

        // Select the group with lowest score (most difficult)
        var lowestScoreGroup = scoreGroups.First();
        var selectedCandidates = lowestScoreGroup.Select(s => s.Word).ToList();

        // If multiple candidates have same score, pick first one
        var selectedAnswer = selectedCandidates.First();
        var feedback = WordleGameLogic.EvaluateGuess(currentGuess, selectedAnswer);

        return new CheatingWordSelection
        {
            SelectedAnswer = selectedAnswer,
            Feedback = feedback,
            RemainingCandidates = selectedCandidates,
            SelectionReason = $"Selected from {selectedCandidates.Count} candidates with {lowestScoreGroup.Key.Hits} hits, {lowestScoreGroup.Key.Presents} presents"
        };
    }

    /// <summary>
    /// Main cheating logic - processes a guess and returns optimal response
    /// </summary>
    public static CheatingWordSelection ProcessCheatingGuess(
        string guess,
        List<string> allWords,
        List<GuessResult> previousGuesses)
    {
        // Start with all words as candidates
        var candidates = new List<string>(allWords);

        // Filter candidates that match all previous guesses
        if (previousGuesses.Any())
        {
            candidates = FilterMatchingCandidates(candidates, previousGuesses);
        }

        // Remove the current guess from candidates (can't be the answer if guessed)
        candidates.Remove(guess.ToLower());

        // Select optimal answer to maximize difficulty
        return SelectOptimalAnswer(guess, candidates);
    }
}

public class CheatingWordSelection
{
    public string SelectedAnswer { get; set; } = string.Empty;
    public string[] Feedback { get; set; } = Array.Empty<string>();
    public List<string> RemainingCandidates { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
}
