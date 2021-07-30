# StormworksLuaDocsGen

This tool generates .lua files based on an existing Google docs spreadsheet in order to provide intellisense in Visual Studio Code.

Generated based on data from
* Vehicles Lua: https://docs.google.com/spreadsheets/d/1tCvYSzxnr5lWduKlePKg4FerpeKHbKTmwmAxlnjZ_Go/
* Mission Lua: https://docs.google.com/spreadsheets/d/1DkjUjX6DwCBt8IhA43NoYhtxk42_f6JXb-dfxOX9lgg/

Latest files can be found [here](https://github.com/Rene-Sackers/StormworksLuaDocsGen/releases/latest).

## Usage

Made for [Visual Studio](https://code.visualstudio.com/) code with the [Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua).

When editing a mission, open the mission's directory with VS Code via File -> Open Folder...  
Drop the [docs.lua from releases](https://github.com/Rene-Sackers/StormworksLuaDocsGen/releases/latest) file in the same directory as `script.lua`, and viola!

![Screenshot](readme/screenshot.png)

I recommend using the documentation files in combination with [Stormworks Lua Extract](https://github.com/Rene-Sackers/StormworksLuaExtract), in order to be able to write vehicle Lua in VS code with full intellisense support.
