# VSOpenFileFromDir

A plugin for Visual Studio which allows to quickly open a file from a directory (typically the solution root directory) by applying a filter.

It works much like Visual Studio Code's Ctrl-P quick open functionality.

![Open File from Directory](preview.png?raw=true "Open File from Directory")

## Usage

* Tools > Open File from Directory
    * It is recommended that you bind `Tools.OpenFileFromDirectory` to Ctrl-P
* Type to filter files from the directory
* Press Up or Down to select a file from the filtered list.
* Press Enter to open the selected file.
* Press Escape to cancel.

## Configuration

The extension tries to read `VSOpenFileFromDirFilters.json` from the root directory. If it exists, the extension looks for keys `"dirs"` and `"files"` inside and loads filters from there. Those filters are directories and files *to be ignored*. 

## Example configuration:

```
{
    "dirs": [".git", ".vs", "out", "bin", "obj"],
    "files": ["*.tar", "*.zip"]
}
```

These filters will ignore all files which are in subdirectories `.git`, `.vs`, `out`, `bin`, and `obj`. And all files with extensions `.zip` and `.tar`

There is also an example configuration file in this repository which his suitable for .NET projects

## Default configuration

If there is no configuration file provided, the ignored directories are `.git` and `.vs`, and the ignored files are `*.sln`

## Intallation

[Download](https://marketplace.visualstudio.com/items?itemName=ibob.OpenFileFromDir) from the Visual Studio Marketplace

## Roadmap

These features are planned for the near future:

* Match Visual Studio's current style

## License

MIT. See accompanying LICENSE file
