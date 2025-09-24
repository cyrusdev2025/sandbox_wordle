# Wordle Game

## Task 1
task 1 is a C# console application that implements a wordle game.
- The application should load environment variables from a .env file.(configuration)
- The application should have a maximum number of trials defined by the environment variable MAXIMUM_TRIALS.
- The application should have a list of words defined by the environment variable WORDS.
- The application should select a random word from the list of words.
- The application should prompt the user to enter a word.
- The application should check if the word is correct.
- The application should display the result of the check.(Hit, Miss, Present)

## setup instruction
- navigate to the task1 directory
- dotnet run

## Task 2
task 2 is a client and server side application. 
- The server side is dotnet core web api
- The client side is angular
- There are configuration files for both the server and client side, hence you need to modify for both of them.
- Session is implemented using memory cache. 
- Memory cache is used to store the session data, and the cleanup will be handled by the framework.
- User can resume to the game if it has a valid session in the local storage => redirect to the game page. 

## setup instruction
- navigate to the task2\backend directory
- dotnet run
- navigate to the task2\frontend directory
- npm install
- ng serve

## Task 3
task 3 is implementing a cheating mode in the game.
- The cheating mode is implemented in the backend.
- The system will select the best answer from the candidates to maximize difficulty.
- score is calculated based on the number of hits and presents. (no of hit * 2 + no of present)

## setup instruction
- navigate to the task2\backend directory
- dotnet run
- navigate to the task2\frontend directory
- npm install
- ng serve

## Task 4
task 4 is implementing a multiplayer mode in the game.
- Multiplayer mode is for 2 players to play against each other.(only support for 2 players at most)
- The players will take turn to guess the same word.
- The system will select the best answer from the candidates to maximize difficulty.(cheating mode)
- score is calculated based on the number of hits and presents. (no of hit * 2 + no of present)
- The game will be overed when one of the players guessed the word correctly or the maximum number of trials is reached.

## setup instruction
- navigate to the task2\backend directory
- dotnet run
- navigate to the task2\frontend directory
- npm install
- ng serve

## Features
- support single player mode or multplayer mode
- single player mode implement session to resume the game
- system running in host cheating mode to maximumize the difficulty
- multplayer mode implement signalR to real time update the game state(feedback of opponents)

## Design trade-offs
- multplayer mode only supports 2 players at most rather than a server to handle multiple games 
=> easier for implementation (no need for handling multiple games' state and players' state)
=> less resource usage (as the server operate without a database, the in-memory cache will be heavier to handle multiple games)

## Future Work
- implement a database for handling multiple games(games' state and players' state)
- room matching for multiplayer mode(room code for each game)
- add a highest score board for past games