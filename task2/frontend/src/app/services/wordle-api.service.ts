import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface StartGameRequest {
    sessionId?: string;
  }

  
export interface StartGameResponse {
  sessionId: string;
  maxTrials: number;
  expiresAt: string;
}

export interface ValidateSessionResponse {
  sessionId: string;
  isValid: boolean;
  errorMessage?: string;
  maxTrials: number;
  remainingTrials: number;
  isCompleted: boolean;
  isWon: boolean;
  triedGuesses: string[];
  guessFeedback: string[][];
  usedCharacters: string[];
  expiresAt: string;
  score: number;
}

export interface SubmitGuessRequest {
  sessionId: string;
  guess: string;
}

export interface SubmitGuessResponse {
  feedback: string[];
  isCorrect: boolean;
  isGameCompleted: boolean;
  isWon: boolean;
  remainingTrials: number;
  usedCharacters: string[];
  score: number;
}

@Injectable({
  providedIn: 'root'
})
export class WordleApiService {
  private readonly apiUrl = environment.apiHost; // Backend API URL

  constructor(private http: HttpClient) {}

  async startGameAsync(): Promise<StartGameResponse> {
    return firstValueFrom(this.http.post<StartGameResponse>(`${this.apiUrl}/start`, {}));
  }

  async validateSessionAsync(sessionId: string): Promise<ValidateSessionResponse> {
    return firstValueFrom(this.http.get<ValidateSessionResponse>(`${this.apiUrl}/validate/${sessionId}`));
  }

  async submitGuessAsync(request: SubmitGuessRequest): Promise<SubmitGuessResponse> {
    return firstValueFrom(this.http.post<SubmitGuessResponse>(`${this.apiUrl}/submit`, request));
  }

  // LocalStorage helpers
  saveSessionId(sessionId: string): void {
    localStorage.setItem('wordle_session_id', sessionId);
  }

  getSessionId(): string | null {
    return localStorage.getItem('wordle_session_id');
  }

  clearSession(): void {
    localStorage.removeItem('wordle_session_id');
  }
}
