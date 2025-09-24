import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

// Multiplayer interfaces
export interface JoinGameRequest {
  playerName: string;
}

export interface JoinGameResponse {
  gameId: string;
  playerId: string;
  playerName: string;
  isWaiting: boolean;
  gameState?: GameState;
}

export interface GameState {
  gameId: string;
  players: Player[];
  status: string; // Waiting, InProgress, Completed
  maxTrials: number;
  winnerId?: string;
}

export interface Player {
  playerId: string;
  playerName: string;
  connectionId: string;
  hasFinished: boolean;
  score: number; // Best score achieved
  bestRound: number; // Round where best score was achieved
  guessCount: number;
  guesses: string[];
  guessFeedback: string[][];
}

export interface MultiplayerGuessRequest {
  gameId: string;
  playerId: string;
  guess: string;
}

export interface MultiplayerGuessResponse {
  feedback: string[];
  isCorrect: boolean;
  isGameCompleted: boolean;
  isWon: boolean;
  remainingTrials: number;
  score: number;
  winnerId?: string;
  
  // Turn-based properties
  isWaitingForOpponent: boolean; // True if waiting for opponent to submit
  opponentGuess?: string; // Opponent's guess (if both submitted)
  opponentFeedback: string[]; // Opponent's feedback
}

export interface OpponentUpdate {
  playerName: string;
  guessNumber: number;
  feedbackColors: string[];
  score: number;
  bestRound: number;
  hasFinished: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class MultiplayerApiService {
  private readonly apiUrl = environment.apiHost;

  constructor(private http: HttpClient) {}

  // Join a multiplayer game (auto-matchmaking)
  joinGame(request: JoinGameRequest) {
    return this.http.post<JoinGameResponse>(`${this.apiUrl}/multiplayer/join`, request);
  }

  // Get game state
  getGameState(gameId: string) {
    return this.http.get<GameState>(`${this.apiUrl}/multiplayer/game/${gameId}`);
  }

  // Submit a guess in multiplayer mode
  submitGuess(request: MultiplayerGuessRequest) {
    return this.http.post<MultiplayerGuessResponse>(`${this.apiUrl}/multiplayer/guess`, request);
  }
}
