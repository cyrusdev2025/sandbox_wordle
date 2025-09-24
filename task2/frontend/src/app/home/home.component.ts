import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { WordleApiService, StartGameRequest } from '../services/wordle-api.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  isLoading = false;
  errorMessage: string | null = null;
  maxTrials = environment.maxTrials;

  private wordleApiService = inject(WordleApiService);
  private router = inject(Router);

  async ngOnInit() {
    // Check if there's an existing sessionId in localStorage
    const existingSessionId = this.wordleApiService.getSessionId();
    
    if (existingSessionId) {
      this.isLoading = true;
      
      try {
        // Validate the existing session
        const validationResponse = await this.wordleApiService.validateSessionAsync(existingSessionId);
        
        if (validationResponse.isValid) {
          // Session is valid, redirect to game page
          console.log('Valid session found, redirecting to game:', existingSessionId);
          this.router.navigate(['/game']);
          return;
        } else {
          // Session is invalid, remove from localStorage
          console.log('Invalid session found, removing from localStorage:', existingSessionId);
          this.wordleApiService.clearSession();
        }
      } catch (error) {
        console.error('Failed to validate session:', error);
        // Remove invalid session from localStorage
        this.wordleApiService.clearSession();
      } finally {
        this.isLoading = false;
      }
    }
  }

  async startNewGame() {
    this.isLoading = true;
    this.errorMessage = null;

    try {
      const response = await this.wordleApiService.startGameAsync();
      
      console.log('Game started with session ID:', response.sessionId);
      
      // Save the session ID to localStorage
      this.wordleApiService.saveSessionId(response.sessionId);
      
      // Navigate to game page
      this.router.navigate(['/game']);
      
    } catch (error) {
      console.error('Failed to start game:', error);
      this.errorMessage = 'Failed to start game. Please try again.';
    } finally {
      this.isLoading = false;
    }
  }

  // Navigate to multiplayer
  startMultiplayer() {
    this.router.navigate(['/multiplayer']);
  }
}
