[![Documentation](https://img.shields.io/badge/documentation-brightgreen.svg)](https://miragenet.github.io/Mirage/)
[![Discord](https://img.shields.io/discord/809535064551456888.svg)](https://discordapp.com/invite/DTBPBYvexy)
[![release](https://img.shields.io/github/release/MirageNet/FizzySteamyMirror.svg)](https://github.com/MirageNet/FizzySteamyMirror/releases/latest)
[![openupm](https://img.shields.io/npm/v/com.miragenet.steamy?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.miragenet.steamy/)
[![GitHub issues](https://img.shields.io/github/issues/MirageNet/FizzySteamyMirror.svg)](https://github.com/MirageNet/FizzySteamyMirror/issues)
![GitHub last commit](https://img.shields.io/github/last-commit/MirageNet/FizzySteamyMirror.svg) ![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg)

[![Build](https://github.com/MirageNet/FizzySteamyMirror/workflows/CI/badge.svg)](https://github.com/MirageNet/FizzySteamyMirror/actions?query=workflow%3ACI)

SteamyMirage

SteamyMirage brings together Steam and Mirage utilising Async of a Steam P2P or UDP network transport layer for Mirage.
Dependencies

Both of these projects need to be installed and working before you can use this transport.

    SteamWorks.NET SteamyMirage relies on Steamworks.NET to communicate with the Steamworks API. Requires .Net 4.x
    Mirage FizzySteamworks is also obviously dependant on Mirage.
    
FizzySteam is only for 64bit version. If you require 32bit you will need to find the dlls yourself.

## Installation
The preferred installation method is Unity Package manager.

If you are using unity 2020.3 or later: 

1) Open your project in unity
2) Install [Mirage](https://github.com/MirageNet/Mirage)
3) Install [Steamworks.net](https://steamworks.github.io/installation)
4) Click on Windows -> Package Manager
5) Click on the plus sign on the left and click on "Add package from git URL..."
6) enter https://github.com/MirageNet/FizzySteamyMirror.git?path=/Assets/Mirage/Runtime/Transport/FizzySteamyMirror
7) Unity will download and install Mirage SteamyMirage

Note: The default 480(Spacewar) appid is a very grey area, technically, it's not allowed but they don't really do anything about it. When you have your own appid from steam then replace the 480 with your own game appid. If you know a better way around this please make a Issue ticket.
Host

To be able to have your game working you need to make sure you have Steam running in the background. SteamManager will print a Debug Message if it initializes correctly.
Client

Before sending your game to your buddy make sure you have your steamID64 ready.

    Send the game to your buddy.
    Your buddy needs your steamID64 to be able to connect.
    Place the steamID64 into "localhost" then click "Client"
    Then they will be connected to you.

Thanks to all developers of the original code for this work. I have just made it work for Mirage the original creators deserve the thanks
