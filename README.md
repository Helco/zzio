# ZanZarah Engine Reimplementation

This is a project to reimplement the engine of ZanZarah: The Hidden Portal by Funatics. It does not contain any game assets, to be able to use this project one has to **buy** the original game (e.g. [on Steam](https://store.steampowered.com/app/384570/Zanzarah_The_Hidden_Portal/) or an old CD).

Apart from replacing the original engine (at some point), this project contains various tools to inspect and/or modify the game assets.

> Currently the game is not yet playable.

## Build instructions

For developers who would like to contribute to this project:

  - Install a recent [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download)
  - Run `./get-dependencies.sh` in the root repository folder
  - Now you should be able to build `zzio.sln` using the `dotnet` CLI or your favorite IDE

*Keep in mind, that only Windows is tested regularly at the moment and several changes might be required to run this on Linux or MacOS.* 

## License

This project is licensed using [MIT](https://opensource.org/licenses/MIT).
All dependencies shall be licensed under similar permissive terms.
