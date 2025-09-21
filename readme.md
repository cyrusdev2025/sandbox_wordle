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