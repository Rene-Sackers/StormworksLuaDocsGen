# StormworksLuaDocsGen

This tool generates .lua files based on an existing Google docs spreadsheet in order to provide intellisense in Visual Studio Code.

Generated based on data from
* Vehicles Lua: https://docs.google.com/spreadsheets/d/1tCvYSzxnr5lWduKlePKg4FerpeKHbKTmwmAxlnjZ_Go/
* Mission Lua: https://docs.google.com/spreadsheets/d/1DkjUjX6DwCBt8IhA43NoYhtxk42_f6JXb-dfxOX9lgg/

Latest files can be found [here](https://github.com/Rene-Sackers/StormworksLuaDocsGen/releases/latest).

## Usage

Made for [Visual Studio](https://code.visualstudio.com/) code with the [Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua).

When editing a mission (or extracted vehicle Lua) script, open the mission's directory with VS Code via File -> Open Folder...  
Drop the docs-missions.lua or docs-vehicles.lua from [the latest release](https://github.com/Rene-Sackers/StormworksLuaDocsGen/releases/latest) file in the same directory as the script you're working on, and viola!

![Screenshot](readme/screenshot.png)

If you're working with vehicle Lua, I recommend using [Stormworks Lua Extract](https://github.com/Rene-Sackers/StormworksLuaExtract). It will read and write the Lua in your vehicles directly to your disk as .lua files, allowing you to edit them in VS code, and simply reload the vehicle in the editor to get your latest changes.
