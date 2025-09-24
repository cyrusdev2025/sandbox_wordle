import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection | null = null;
  private isConnected = false;

  constructor() {}

  // Start SignalR connection
  public startConnection(): Promise<void> {
    if (this.hubConnection && this.isConnected) {
      return Promise.resolve();
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiHost}/multiplayerhub`, {
        withCredentials: true
      })
      .withAutomaticReconnect()
      .build();

    return this.hubConnection.start().then(() => {
      console.log('SignalR Connected');
      this.isConnected = true;
    }).catch(err => {
      console.error('Error while starting SignalR connection: ', err);
      throw err;
    });
  }

  // Stop SignalR connection
  public stopConnection(): Promise<void> {
    if (this.hubConnection) {
      return this.hubConnection.stop().then(() => {
        console.log('SignalR Disconnected');
        this.isConnected = false;
      });
    }
    return Promise.resolve();
  }

  // Join a game group
  public joinGame(gameId: string, playerId: string): Promise<void> {
    if (this.hubConnection && this.isConnected) {
      console.log(`Joining game: ${gameId} as player: ${playerId}`);
      return this.hubConnection.invoke('JoinGame', gameId, playerId);
    }
    return Promise.reject('SignalR connection not established');
  }

  // Leave a game group
  public leaveGame(gameId: string): Promise<void> {
    if (this.hubConnection && this.isConnected) {
      console.log(`Leaving game: ${gameId}`);
      return this.hubConnection.invoke('LeaveGame', gameId);
    }
    return Promise.reject('SignalR connection not established');
  }

  // Get connection status
  public get connectionState(): signalR.HubConnectionState {
    return this.hubConnection?.state || signalR.HubConnectionState.Disconnected;
  }

  // Check if connected
  public get connected(): boolean {
    return this.isConnected && this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  // Event listener methods for components to subscribe to SignalR events
  public onGameStarted(callback: (gameState: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('GameStarted', callback);
    }
  }

  public onOpponentGuessUpdate(callback: (update: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('OpponentGuessUpdate', callback);
    }
  }

  public onGameCompleted(callback: (result: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('GameCompleted', callback);
    }
  }

  public onRoundCompleted(callback: (roundData: any) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on('RoundCompleted', callback);
    }
  }

  // Remove event listeners (useful for cleanup)
  public offGameStarted(): void {
    if (this.hubConnection) {
      this.hubConnection.off('GameStarted');
    }
  }

  public offOpponentGuessUpdate(): void {
    if (this.hubConnection) {
      this.hubConnection.off('OpponentGuessUpdate');
    }
  }

  public offGameCompleted(): void {
    if (this.hubConnection) {
      this.hubConnection.off('GameCompleted');
    }
  }
}
