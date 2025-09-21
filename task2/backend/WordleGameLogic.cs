using System.Linq;

public static class WordleGameLogic
{
    public static bool IsValidWord(string word)
    {
        return word.All(char.IsLetter);
    }

    public static string[] EvaluateGuess(string guess, string answer)
    {
        string[] result = new string[5];
        char[] answerChars = answer.ToCharArray();
        char[] guessChars = guess.ToCharArray();
        
        // Step 1: Mark all exact matches (correct) first
        for (int i = 0; i < 5; i++)
        {
            if (guessChars[i] == answerChars[i])
            {
                result[i] = "correct";
                answerChars[i] = '*'; // Mark as used
                guessChars[i] = '*';  // Mark as processed
            }
        }
        
        // Step 2: Check remaining letters for present/absent
        for (int i = 0; i < 5; i++)
        {
            if (result[i] == null) // Not correct
            {
                bool found = false;
                for (int j = 0; j < 5; j++)
                {
                    if (answerChars[j] == guessChars[i])
                    {
                        result[i] = "present";
                        answerChars[j] = '*'; // Mark as used
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    result[i] = "miss";
                }
            }
        }
        
        return result;
    }

    public static string SelectRandomWord(string[] words)
    {
        Random rng = new Random();
        int randomIndex = rng.Next(0, words.Length);
        return words[randomIndex];
    }
}
