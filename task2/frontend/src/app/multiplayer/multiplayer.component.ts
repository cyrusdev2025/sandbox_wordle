import { Component, inject, OnInit, OnDestroy, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MultiplayerApiService, JoinGameRequest, GameState, Player, MultiplayerGuessRequest, OpponentUpdate } from '../services/multiplayer-api.service';
import { SignalRService } from '../services/signalr.service';

@Component({
  selector: 'app-multiplayer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './multiplayer.component.html',
  styleUrls: ['./multiplayer.component.scss']
})
export class MultiplayerComponent implements OnInit, OnDestroy {
  // Game state
  gameId = '';
  playerId = '';
  playerName = '';
  gameState: GameState | null = null;
  currentGuess = '';
  maxTrials = 6;
  currentView: 'join' | 'waiting' | 'playing' | 'waitingForOpponent' | 'completed' = 'join';
  keyboardState: { [key: string]: string } = {};
  
  // Opponent data
  opponentGuesses: string[][] = []; // Array of opponent's guess feedback colors
  
  // Turn-based state
  isWaitingForOpponent = false;
  hasSubmittedCurrentRound = false;
  mySubmittedGuess = ''; // Store my guess while waiting for opponent
  
  // Form data
  inputPlayerName = '';
  isLoading = false;
  errorMessage = '';
  
  // Services
  private multiplayerApi = inject(MultiplayerApiService);
  private signalR = inject(SignalRService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  async ngOnInit() {
    // Initialize SignalR
    await this.initializeSignalR();
  }

  ngOnDestroy() {
    // Cleanup SignalR
    this.signalR.offGameStarted();
    this.signalR.offOpponentGuessUpdate();
    this.signalR.offGameCompleted();
    
    if (this.gameId) {
      this.signalR.leaveGame(this.gameId).catch(err => {
        console.error('Error leaving game:', err);
      });
    }
    
    this.signalR.stopConnection().catch(err => {
      console.error('Error stopping SignalR:', err);
    });
  }

  // Initialize SignalR connection
  private async initializeSignalR(): Promise<void> {
    try {
      console.log('🔌 Initializing SignalR...');
      await this.signalR.startConnection();
      
      // Set up event listeners
      this.setupSignalREventListeners();
      
      console.log('✅ SignalR initialized');
    } catch (error) {
      console.error('❌ SignalR initialization failed:', error);
      this.errorMessage = 'Failed to connect to game server';
    }
  }

  // Set up SignalR event listeners
  private setupSignalREventListeners(): void {
    // Listen for game start
    this.signalR.onGameStarted((gameState: GameState) => {
      console.log('🚀 Game started:', gameState);
      this.gameState = gameState;
      this.currentView = 'playing';
      this.maxTrials = gameState.maxTrials;
      
      console.log('🎮 Initial current player:', this.currentPlayer);
      console.log('🎮 Initial opponent:', this.opponent);
      console.log('🎮 Game state players:', this.gameState?.players);
      
      // Initialize opponent guess count if not set
      if (this.opponent && this.opponent.guessCount === undefined) {
        this.opponent.guessCount = 0;
      }
      
      this.cdr.detectChanges();
    });

    // Listen for opponent updates
    this.signalR.onOpponentGuessUpdate((update: OpponentUpdate) => {
      console.log('👥 Opponent guess update:', update);
      
      // If we're waiting for opponent, this means both players have now submitted
      if (this.currentView === 'waitingForOpponent') {
        console.log('🎯 Received opponent update while waiting - both players submitted!');
        this.isWaitingForOpponent = false;
        this.hasSubmittedCurrentRound = false;
        this.currentView = 'playing';
        
        // We need to also add our own guess to the game state since we were waiting
        // The server should have processed our guess, but we need to update the UI
        if (this.currentPlayer && this.mySubmittedGuess) {
          // Note: We don't have our own feedback yet, it should come from the server response
          // This will be handled by the guess response when it eventually comes
          console.log('🔄 Transitioning from waiting to playing, submitted guess:', this.mySubmittedGuess);
        }
      }
      
      // Process opponent update regardless of view state
      // Add opponent's guess colors (no letters, just colors)
      // Ensure we have the right number of rows
      while (this.opponentGuesses.length < update.guessNumber) {
        this.opponentGuesses.push(['', '', '', '', '']); // Empty row placeholder
      }
      // Set the actual feedback colors for this guess
      this.opponentGuesses[update.guessNumber - 1] = update.feedbackColors;
      
      // Update opponent score information
      if (this.opponent) {
        this.opponent.score = update.score;
        this.opponent.bestRound = update.bestRound;
        this.opponent.hasFinished = update.hasFinished;
        this.opponent.guessCount = update.guessNumber;
      }
      
      console.log('👥 Updated opponent guesses:', this.opponentGuesses);
      console.log('🎨 Opponent feedback colors:', update.feedbackColors);
      this.cdr.detectChanges();
    });

    // Listen for round completion (both players get their feedback)
    this.signalR.onRoundCompleted((roundData: any) => {
      console.log('🎯 Round completed:', roundData);
      
      // Ensure we're back in playing view
      this.isWaitingForOpponent = false;
      this.hasSubmittedCurrentRound = false;
      this.currentView = 'playing';
      
      // Update current player's game state with their feedback
      if (this.currentPlayer && roundData.myFeedback) {
        // Check if this guess is already added to avoid duplicates
        const guessExists = this.currentPlayer.guesses.includes(roundData.myGuess);
        
        if (!guessExists) {
          // Add new guess
          this.currentPlayer.guesses.push(roundData.myGuess);
          this.currentPlayer.guessFeedback.push(roundData.myFeedback);
        } else {
          // Find the index and update feedback
          const guessIndex = this.currentPlayer.guesses.indexOf(roundData.myGuess);
          this.currentPlayer.guessFeedback[guessIndex] = roundData.myFeedback;
        }
        
        this.currentPlayer.guessCount = this.currentPlayer.guesses.length;
        this.currentPlayer.score = roundData.myScore;
        this.currentPlayer.hasFinished = roundData.isGameCompleted;
        
        // Update keyboard state
        this.updateKeyboardState(roundData.myGuess.toLowerCase(), roundData.myFeedback);
      }
      
      // No need to store opponent round data since we don't display it
      
      // Clear submitted guess
      this.mySubmittedGuess = '';
      this.currentGuess = '';
      
      this.cdr.detectChanges();
    });

    // Listen for game completion
    this.signalR.onGameCompleted((result: any) => {
      console.log('🏆 Game completed:', result);
      
      // Update final player data with scores and best rounds
      if (result.players && result.players.length === 2) {
        const player1 = result.players[0];
        const player2 = result.players[1];
        
        console.log('🔍 Final player data from server:');
        console.log('Player 1:', player1);
        console.log('Player 2:', player2);
        
        if (this.currentPlayer && player1.playerId === this.currentPlayer.playerId) {
          console.log('📊 Updating current player with server data:', player1);
          this.currentPlayer.score = player1.score;
          this.currentPlayer.bestRound = player1.bestRound;
          this.currentPlayer.guessCount = player1.guessCount;
        } else if (this.currentPlayer && player2.playerId === this.currentPlayer.playerId) {
          console.log('📊 Updating current player with server data:', player2);
          this.currentPlayer.score = player2.score;
          this.currentPlayer.bestRound = player2.bestRound;
          this.currentPlayer.guessCount = player2.guessCount;
        }
        
        if (this.opponent && player1.playerId === this.opponent.playerId) {
          console.log('👥 Updating opponent with server data:', player1);
          this.opponent.score = player1.score;
          this.opponent.bestRound = player1.bestRound;
          this.opponent.guessCount = player1.guessCount;
        } else if (this.opponent && player2.playerId === this.opponent.playerId) {
          console.log('👥 Updating opponent with server data:', player2);
          this.opponent.score = player2.score;
          this.opponent.bestRound = player2.bestRound;
          this.opponent.guessCount = player2.guessCount;
        }
        
        console.log('🎯 Final current player:', this.currentPlayer);
        console.log('🎯 Final opponent:', this.opponent);
        console.log('🎯 My player ID:', this.playerId);
        console.log('🎯 My player name:', this.playerName);
        console.log('🎯 Winner ID:', result.winnerId);
        console.log('🔢 Current player guess count - Server:', this.currentPlayer?.guessCount, 'Array length:', this.currentPlayer?.guesses?.length);
        console.log('🔢 Opponent guess count - Server:', this.opponent?.guessCount, 'Array length:', this.opponent?.guesses?.length);
      }
      
      // Update game state with winner
      if (this.gameState) {
        this.gameState.winnerId = result.winnerId;
      }
      
      this.currentView = 'completed';
      this.cdr.detectChanges();
    });
  }

  // Join game
  async joinGame() {
    if (!this.inputPlayerName.trim()) {
      this.errorMessage = 'Please enter your name';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    try {
      const request: JoinGameRequest = {
        playerName: this.inputPlayerName.trim()
      };

      console.log('🎯 Joining game...', request);
      const response = await this.multiplayerApi.joinGame(request).toPromise();
      
      if (response) {
        console.log('✅ Joined game:', response);
        
        // Store game info
        this.gameId = response.gameId;
        this.playerId = response.playerId;
        this.playerName = response.playerName;
        this.gameState = response.gameState || null;
        
        // Join SignalR group
        await this.signalR.joinGame(this.gameId, this.playerId);
        
        // Update view
        if (response.isWaiting) {
          this.currentView = 'waiting';
        } else {
          this.currentView = 'playing';
          this.maxTrials = this.gameState?.maxTrials || 6;
        }
      }
    } catch (error: any) {
      console.error('❌ Failed to join game:', error);
      this.errorMessage = error.error?.message || 'Failed to join game. Please try again.';
    } finally {
      this.isLoading = false;
    }
  }

  // Keyboard input handling
  @HostListener('window:keydown', ['$event'])
  onKeyPress(event: KeyboardEvent) {
    if (this.currentView !== 'playing' || this.isWaitingForOpponent || this.isGameCompleted) return;

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
    if (this.currentGuess.length < 5 && !this.isGameCompleted) {
      this.currentGuess += letter;
    }
  }

  onBackspace() {
    if (this.currentGuess.length > 0) {
      this.currentGuess = this.currentGuess.slice(0, -1);
    }
  }

  async onSubmitGuess() {
    if (this.currentGuess.length !== 5 || this.isGameCompleted || !this.gameId || !this.playerId) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    try {
      const request: MultiplayerGuessRequest = {
        gameId: this.gameId,
        playerId: this.playerId,
        guess: this.currentGuess
      };

      // Clear previous round results when starting new round
      // (No opponent round data to clear since we don't store it)
      
      console.log('🎯 Submitting guess:', request);
      const response = await this.multiplayerApi.submitGuess(request).toPromise();
      console.log('✅ Received response:', response);
      
      if (response) {
        if (response.isWaitingForOpponent) {
          // Waiting for opponent to submit
          console.log('⏳ Waiting for opponent to submit their guess');
          this.mySubmittedGuess = this.currentGuess; // Store the guess
          this.isWaitingForOpponent = true;
          this.hasSubmittedCurrentRound = true;
          this.currentView = 'waitingForOpponent';
          this.currentGuess = ''; // Clear input
          
          // Don't update game state yet - wait for both submissions
          // The response will come later when both players have submitted
        } else {
          // Both players submitted - process the round
          console.log('🎯 Both players submitted - processing round');
          this.isWaitingForOpponent = false;
          this.hasSubmittedCurrentRound = false;
          this.currentView = 'playing';
          
          // In turn-based mode, we should NOT update game state here
          // The RoundCompleted SignalR event will handle the updates
          console.log('⏳ Waiting for RoundCompleted event to update game state');
          
          this.currentGuess = '';
          this.mySubmittedGuess = ''; // Clear stored guess
          
          // Force UI update
          this.cdr.detectChanges();
        }
      }
    } catch (error: any) {
      console.error('❌ Failed to submit guess:', error);
      this.errorMessage = error.error?.message || 'Failed to submit guess. Please try again.';
    } finally {
      this.isLoading = false;
    }
  }

  // Update keyboard state based on guess feedback
  updateKeyboardState(guess: string, feedback: string[]) {
    for (let i = 0; i < guess.length; i++) {
      const letter = guess[i].toUpperCase();
      const letterFeedback = feedback[i];
      
      // Only update if the new feedback is "better" than existing
      const currentState = this.keyboardState[letter];
      
      // Normalize feedback - backend sends 'miss' instead of 'absent'
      const normalizedFeedback = letterFeedback === 'miss' ? 'absent' : letterFeedback;
      
      if (!currentState || 
          (normalizedFeedback === 'correct') ||
          (normalizedFeedback === 'present' && (currentState === 'absent' || currentState === 'miss'))) {
        this.keyboardState[letter] = normalizedFeedback;
      }
    }
  }

  // Get current player
  get currentPlayer(): Player | undefined {
    return this.gameState?.players.find((p: Player) => p.playerId === this.playerId);
  }

  // Get opponent player
  get opponent(): Player | undefined {
    const opp = this.gameState?.players.find((p: Player) => p.playerId !== this.playerId);
    if (opp) {
      console.log('🔍 Opponent getter called, returning:', opp);
    }
    return opp;
  }

  // Check if game is completed
  get isGameCompleted(): boolean {
    return this.currentPlayer?.hasFinished || false;
  }

  // Get game over message
  getGameOverMessage(): string {
    if (!this.currentPlayer || !this.opponent) {
      return 'Game ended.';
    }

    // Check if current player got the correct answer
    const playerHasCorrectAnswer = this.currentPlayer.guesses.some(guess => 
      this.currentPlayer!.guessFeedback[this.currentPlayer!.guesses.indexOf(guess)]?.every(f => f === 'correct' || f === 'hit')
    );

    // Check if opponent got the correct answer
    const opponentHasCorrectAnswer = this.opponent.hasFinished && this.currentPlayer.hasFinished;

    // Check if it's a tie (no winner)
    if (this.gameState?.winnerId === null || this.gameState?.winnerId === undefined) {
      if (playerHasCorrectAnswer && opponentHasCorrectAnswer) {
        return 'Both players found the answer in the same round - It\'s a tie!';
      } else if (!playerHasCorrectAnswer && !opponentHasCorrectAnswer) {
        return 'Same performance - It\'s a tie!';
      } else {
        return 'It\'s a tie!';
      }
    }

    // Regular win/loss scenarios
    if (playerHasCorrectAnswer && !opponentHasCorrectAnswer) {
      return 'You found the correct answer!';
    } else if (!playerHasCorrectAnswer && opponentHasCorrectAnswer) {
      return `${this.opponent.playerName} found the correct answer!`;
    } else if (playerHasCorrectAnswer && opponentHasCorrectAnswer) {
      return 'Both players found the answer!';
    } else {
      return 'Maximum attempts reached.';
    }
  }

  // Get CSS class for letter based on feedback
  getLetterClass(guessIndex: number, letterIndex: number): string {
    const player = this.currentPlayer;
    if (!player || !player.guessFeedback[guessIndex]) return '';
    
    const feedback = player.guessFeedback[guessIndex][letterIndex];
    switch (feedback) {
      case 'correct': return 'correct';
      case 'hit': return 'correct'; // Backend might send 'hit'
      case 'present': return 'present';
      case 'absent': return 'absent';
      case 'miss': return 'absent'; // Backend sends 'miss' instead of 'absent'
      default: return '';
    }
  }

  // Normalize opponent feedback for CSS classes
  normalizeOpponentFeedback(feedback: string): string {
    if (!feedback) return 'empty';
    
    switch (feedback) {
      case 'correct': return 'correct';
      case 'hit': return 'correct'; // Backend might send 'hit'
      case 'present': return 'present';
      case 'absent': return 'absent';
      case 'miss': return 'absent'; // Backend sends 'miss' instead of 'absent'
      default: return 'empty';
    }
  }

  // Get CSS class for opponent's guess (colors only) - keeping for compatibility
  getOpponentLetterClass(guessIndex: number, letterIndex: number): string {
    if (!this.opponentGuesses[guessIndex]) return '';
    
    const feedback = this.opponentGuesses[guessIndex][letterIndex];
    return this.normalizeOpponentFeedback(feedback);
  }

  // Get CSS class for keyboard key based on its state
  getKeyClass(letter: string): string {
    const state = this.keyboardState[letter.toUpperCase()];
    switch (state) {
      case 'correct': return 'key-correct';
      case 'present': return 'key-present';
      case 'absent': return 'key-absent';
      case 'miss': return 'key-absent'; // Backend sends 'miss'
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
    const currentPlayerGuesses = this.currentPlayer?.guesses.length || 0;
    const emptyRowCount = this.maxTrials - currentPlayerGuesses - (this.isGameCompleted ? 0 : 1);
    return Array(Math.max(0, emptyRowCount)).fill(0).map((_, i) => i);
  }

  // Helper method for opponent empty rows
  getOpponentEmptyRows(): number[] {
    const opponentGuesses = this.opponentGuesses.length;
    const emptyRowCount = this.maxTrials - opponentGuesses;
    return Array(Math.max(0, emptyRowCount)).fill(0).map((_, i) => i);
  }

  // Helper methods for template Array usage
  getEmptyLetterCells(): number[] {
    return Array(5 - this.currentGuess.length).fill(0).map((_, i) => i);
  }

  getFiveEmptyCells(): number[] {
    return Array(5).fill(0).map((_, i) => i);
  }

  // Calculate score for a round based on feedback
  calculateRoundScore(feedback: string[]): number {
    let score = 0;
    for (const result of feedback) {
      if (result === 'correct') score += 2;
      else if (result === 'present') score += 1;
      // absent = 0 points
    }
    return score;
  }

  // Go back to home
  goHome() {
    this.router.navigate(['/home']);
  }

  // Start new game
  startNewGame() {
    // Reset state
    this.gameId = '';
    this.playerId = '';
    this.playerName = '';
    this.gameState = null;
    this.currentGuess = '';
    this.keyboardState = {};
    this.opponentGuesses = [];
    this.inputPlayerName = '';
    this.errorMessage = '';
    
    // Reset turn-based state
    this.isWaitingForOpponent = false;
    this.hasSubmittedCurrentRound = false;
    this.mySubmittedGuess = '';
    
    // Go back to join view
    this.currentView = 'join';
  }

  // Debug method to check current state
  debugGameState() {
    console.log('=== GAME STATE DEBUG ===');
    console.log('Current Player:', this.currentPlayer);
    console.log('Current Player Guesses:', this.currentPlayer?.guesses);
    console.log('Current Player Feedback:', this.currentPlayer?.guessFeedback);
    console.log('Opponent Guesses Array:', this.opponentGuesses);
    console.log('Keyboard State:', this.keyboardState);
    console.log('Current View:', this.currentView);
    console.log('========================');
  }
}
