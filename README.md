# SatisfactorySyncer
A synchronization tool for [Satisfactory](https://store.steampowered.com/app/526870/Satisfactory/) to share savegames in a cloud storage.

## Why
In Satisfactory there is no 'real' multiplayer mode. But it is possible to start a local session and invite other players to join it. It is still buggy, but it does, what it does.
So far the only option to have something like a multiplayer experience is to have a dedicated PC running the game 24/7 and everybody can join as he wants to. This is inefficient as no one can play on the server and you don't want to have it running all the time. So if player 1 starts the game with player 2 joining, player 2 always has to wait for player 1 to start the game before he can join. Fortunately it is possible to share savegames! "Just" copy them to another PC with the game installed an load the game. If you share the current state with your mates using a cloudstorage, anyone can start the game in the most recent state when everybody else is offline. But there are some nasty file operations to be done and you always have to check dates and please do never overwrite stuff, as you and your friends can lose hours of work.

## How it works
The [SatisfactorySyncer](https://github.com/NilesDavis/SatisfactorySyncer/) helps you keep track of all this. It takes the selected session (and the savegames belonging to it) and pushes them to a cloudstorage, or it pulls the recent state from the cloudstorage to your local game folder while helping you with dates and times.

## How to use
Just install the latest version, select your local savegames folder and your local path to the cloud storage. Pull and enjoy!
