using System;
using System.Collections.Generic;
using System.Linq;
using DotNetEnv;

public class Program
{
    public static void Main(string[] args)
    {
        // Load environment variables
        Env.Load();

        // init environment variables with null checks
        string? maxTrialsEnv = Environment.GetEnvironmentVariable("MAXIMUM_TRIALS");
        int MAXIMUM_TRIALS = !string.IsNullOrEmpty(maxTrialsEnv) ? int.Parse(maxTrialsEnv) : 3;

        string? wordsEnv = Environment.GetEnvironmentVariable("WORDS");
        string[] WORDS;
        if (!string.IsNullOrEmpty(wordsEnv))
        {
            WORDS = wordsEnv.Split(',');
        }
        else
        {
            WORDS = new string[] { "apple", "girls", "sunny" };
        }

        Random rng = new Random();
        int randomIndex = rng.Next(0, WORDS.Length);
        string word = WORDS[randomIndex]; //random word
        HashSet<char> uniqueCharSet = new HashSet<char>(word); //unique characters
        Console.WriteLine("Welcome to wordle game");
        bool isWin = false;
        
        for(int i = MAXIMUM_TRIALS; i > 0; i--)
        {
            Console.WriteLine("Trial " + i);
            Console.Write("Please enter a word: ");
            string? guess = Console.ReadLine();
            if (string.IsNullOrEmpty(guess)) //null checking
            {
                Console.WriteLine("Please enter a valid word.");
                continue;
            }
            guess = guess.ToLower().Trim();

            // Input validation
            if (guess.Length != 5)
            {
                Console.WriteLine("Please enter exactly 5 letters.");
                i++; // Don't consume a trial for invalid input
                continue;
            }

            if (!IsValidWord(guess))
            {
                Console.WriteLine("Please enter only alphabetic characters.");
                i++; // Don't consume a trial for invalid input
                continue;
            }

            if (guess == word)
            {
                Console.WriteLine("🎉 You win!");
                isWin = true;
            }
            else
            {
                string[] feedback = EvaluateGuess(guess, word);
                
                // Display visual feedback
                Console.Write("Result: ");
                for (int pos = 0; pos < 5; pos++)
                {
                    string symbol = feedback[pos] switch
                    {
                        "Hit" => "🟩",      
                        "Present" => "🟨",  
                        "Miss" => "⬜",     
                        _ => "❓"
                    };
                    Console.Write($"{guess[pos].ToString().ToUpper()}{symbol} ");
                }
                Console.WriteLine(); //newline
                Console.WriteLine($"Feedback: {string.Join(" ", feedback)}");
            }
            if (isWin) break; // Exit loop if player wins
        }
        
        if(!isWin)
        {
            Console.WriteLine($"You Lose! The word was: {word.ToUpper()}");
        }
    }

    public static bool IsValidWord(string word)
    {
        return word.All(char.IsLetter);
    }

    public static string[] EvaluateGuess(string guess, string answer)
    {
        string[] result = new string[5];
        char[] answerChars = answer.ToCharArray();
        char[] guessChars = guess.ToCharArray();
        
        // Step 1: Mark all exact matches (Hits) first
        for (int i = 0; i < 5; i++)
        {
            if (guessChars[i] == answerChars[i])
            {
                result[i] = "Hit";
                answerChars[i] = '*'; // Mark as used
                guessChars[i] = '*';  // Mark as processed
            }
        }
        
        // Step 2: Check remaining letters for Present/Miss
        for (int i = 0; i < 5; i++)
        {
            if (result[i] == null) // Not a Hit
            {
                bool found = false;
                for (int j = 0; j < 5; j++)
                {
                    if (answerChars[j] == guessChars[i])
                    {
                        result[i] = "Present";
                        answerChars[j] = '*'; // Mark as used
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    result[i] = "Miss";
                }
            }
        }
        
        return result;
    }
}
