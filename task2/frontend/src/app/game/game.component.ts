import { Component, inject, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { WordleApiService } from '../services/wordle-api.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './game.component.html',
  styleUrl: './game.component.scss'
})
export class GameComponent implements OnInit, OnDestroy {
  sessionId: string = '';
  currentGuess: string = '';
  guesses: string[] = [];
  guessFeedback: string[][] = []; // Store feedback for each guess
  keyboardState: { [key: string]: string } = {}; // Track keyboard key states
  maxTrials: number = environment.maxTrials;
  remainingTrials: number = environment.maxTrials;
  gameOver: boolean = false;
  isWon: boolean = false;
  isLoading: boolean = false;
  errorMessage: string | null = null;
  showPopup: boolean = false;
  popupTitle: string = '';
  popupMessage: string = '';
  correctAnswer: string = '';
  currentScore: number = 0;
  highestScore: number = 0;

  private wordleApiService = inject(WordleApiService);
  private router = inject(Router);

  constructor() {
    // Simply get session ID for display - no validation here
    this.sessionId = this.wordleApiService.getSessionId() || '';
    
    // Load highest score from localStorage
    this.highestScore = this.getHighestScore();
    
    // If no session ID, redirect to home
    if (!this.sessionId) {
      this.router.navigate(['/home']);
    }
  }

  async loadSessionData() {
    try {
      const validationResponse = await this.wordleApiService.validateSessionAsync(this.sessionId);
      
      if (!validationResponse.isValid) {
        // Session is invalid or completed, clear and redirect
        this.wordleApiService.clearSession();
        this.router.navigate(['/home']);
        return;
      }

      // Load session data
      this.maxTrials = validationResponse.maxTrials;
      this.remainingTrials = validationResponse.remainingTrials;
      this.gameOver = validationResponse.isCompleted;
      this.isWon = validationResponse.isWon;
      this.currentScore = validationResponse.score;
      
      // Load past guesses and feedback
      this.guesses = validationResponse.triedGuesses || [];
      this.guessFeedback = validationResponse.guessFeedback || [];
      
      // Rebuild keyboard state from past guesses
      this.rebuildKeyboardState();
      
      // If game is completed, show appropriate popup
      if (this.gameOver) {
        if (this.isWon) {
          this.showWinPopup();
        } else {
          this.showLosePopup();
        }
      }
      
    } catch (error) {
      console.error('Failed to load session data:', error);
      // On error, clear session and redirect
      this.wordleApiService.clearSession();
      this.router.navigate(['/home']);
    }
  }

  rebuildKeyboardState() {
    // Reset keyboard state
    this.keyboardState = {};
    
    // Rebuild keyboard state from all past guesses and their feedback
    for (let i = 0; i < this.guesses.length; i++) {
      const guess = this.guesses[i];
      const feedback = this.guessFeedback[i];
      
      if (guess && feedback) {
        this.updateKeyboardState(guess, feedback);
      }
    }
  }

  async ngOnInit() {
    // Load existing session data if available
    if (this.sessionId) {
      await this.loadSessionData();
    }
  }

  ngOnDestroy() {
    // Cleanup if needed
  }

  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    if (this.gameOver) return;

    const key = event.key.toUpperCase();
    
    // Handle letter keys (A-Z)
    if (key.match(/^[A-Z]$/) && this.currentGuess.length < 5) {
      this.onLetterClick(key.toLowerCase());
      event.preventDefault();
    }
    // Handle Enter key
    else if (key === 'ENTER') {
      this.onSubmitGuess();
      event.preventDefault();
    }
    // Handle Backspace key
    else if (key === 'BACKSPACE') {
      this.onBackspace();
      event.preventDefault();
    }
  }

  onLetterClick(letter: string) {
    if (this.currentGuess.length < 5 && !this.gameOver) {
      this.currentGuess += letter;
    }
  }

  onBackspace() {
    if (this.currentGuess.length > 0) {
      this.currentGuess = this.currentGuess.slice(0, -1);
    }
  }

  async onSubmitGuess() {
    if (this.currentGuess.length !== 5 || this.gameOver || !this.sessionId) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    try {
      const response = await this.wordleApiService.submitGuessAsync({
        sessionId: this.sessionId,
        guess: this.currentGuess
      });

      // Add guess and feedback to arrays
      this.guesses.push(this.currentGuess);
      this.guessFeedback.push(response.feedback);
      this.updateKeyboardState(this.currentGuess, response.feedback);
      this.remainingTrials = response.remainingTrials;
      this.currentScore = response.score;
      this.currentGuess = '';

      // Check game outcome
      if (response.isCorrect) {
        this.gameOver = true;
        this.isWon = true;
        this.updateHighestScore();
        this.showWinPopup();
        // Clear session from localStorage when game is won
        this.wordleApiService.clearSession();
      } else if (response.isGameCompleted || this.remainingTrials <= 0) {
        this.gameOver = true;
        this.isWon = false;
        this.correctAnswer = '';
        this.updateHighestScore();
        this.showLosePopup();
        // Clear session from localStorage when game is lost
        this.wordleApiService.clearSession();
      }

    } catch (error) {
      console.error('Failed to submit guess:', error);
      this.errorMessage = 'Failed to submit guess. Please try again.';
    } finally {
      this.isLoading = false;
    }
  }

  startNewGame() {
    // Navigate back to home page
    this.router.navigate(['/home']);
  }

  showWinPopup() {
    this.popupTitle = 'Congratulations! 🎉';
    this.popupMessage = `You guessed the word in ${this.guesses.length} ${this.guesses.length === 1 ? 'try' : 'tries'}!`;
    this.showPopup = true;
  }

  showLosePopup() {
    this.popupTitle = 'Game Over 😔';
    this.popupMessage = this.correctAnswer 
      ? `The word was: ${this.correctAnswer.toUpperCase()}` 
      : 'Better luck next time!';
    this.showPopup = true;
    // Clear session when showing lose popup
    this.wordleApiService.clearSession();
  }

  closePopup() {
    this.showPopup = false;
  }

  tryAgain() {
    this.closePopup();
    this.router.navigate(['/home']);
  }

  // Get CSS class for letter based on feedback
  getLetterClass(guessIndex: number, letterIndex: number): string {
    if (!this.guessFeedback[guessIndex]) return '';
    
    const feedback = this.guessFeedback[guessIndex][letterIndex];
    switch (feedback) {
      case 'correct': return 'correct';
      case 'present': return 'present';
      case 'absent': return 'absent';
      default: return '';
    }
  }

  // Update keyboard state based on guess feedback
  updateKeyboardState(guess: string, feedback: string[]) {
    for (let i = 0; i < guess.length; i++) {
      const letter = guess[i].toUpperCase();
      const letterFeedback = feedback[i];
      
      // Only update if the new feedback is "better" than existing
      // Priority: correct > present > absent
      const currentState = this.keyboardState[letter];
      
      if (!currentState || 
          (letterFeedback === 'correct') ||
          (letterFeedback === 'present' && currentState === 'absent')) {
        this.keyboardState[letter] = letterFeedback;
      }
    }
  }

  // Get CSS class for keyboard key based on its state
  getKeyClass(letter: string): string {
    const state = this.keyboardState[letter.toUpperCase()];
    switch (state) {
      case 'correct': return 'key-correct';
      case 'present': return 'key-present';
      case 'absent': return 'key-absent';
      default: return '';
    }
  }

  // QWERTY keyboard layout
  get keyboardRows(): string[][] {
    return [
      ['Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P'],
      ['A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L'],
      ['Z', 'X', 'C', 'V', 'B', 'N', 'M']
    ];
  }

  // Helper method for template
  getEmptyRows(): number[] {
    const emptyRowCount = this.maxTrials - this.guesses.length - (this.gameOver ? 0 : 1);
    return Array(Math.max(0, emptyRowCount)).fill(0).map((_, i) => i);
  }

  // Get highest score from localStorage
  getHighestScore(): number {
    const stored = localStorage.getItem('wordle-highest-score');
    return stored ? parseInt(stored, 10) : 0;
  }

  // Update highest score if current score is higher
  updateHighestScore(): void {
    if (this.currentScore > this.highestScore) {
      this.highestScore = this.currentScore;
      localStorage.setItem('wordle-highest-score', this.highestScore.toString());
    }
  }
}
