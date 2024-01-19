# sebuild

A cross-platform [Roslyn](https://github.com/dotnet/roslyn) based build tool for developing user scripts in Keen Software House's Space Engineers.  
Currently supports a variety of code minification options allowing character count reductions of 50-80% observed from my own testing.

## Features
- [x] Project templates with scripting DLL references for IDE integration
- [x] .csproj-based builds supporting library scripts and multi-file development
- [x] Code minification
  - [x] Dead code removal including unused properties and methods within a class
  - [x] Identifier renaming for all user-created definitions
  - [x] Whitespace and comment removal
- [ ] Script templates
- [ ] Out-of-game script manager for renaming and deleting scripts
- [ ] Allowed classes whitelist checker
- [ ] Custom script icon support
- [ ] Automatic detection of installed game files

## 
