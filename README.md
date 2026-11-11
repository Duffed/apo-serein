# Serein

A Windows front end for Equalizer APO with graphic and parametric EQ, an interactive response graph, per-device profiles, Peace profile import, and a searchable AutoEQ catalog.

![Serein in dark mode with the frequency response graph and parametric filters](docs/screenshot.png)

## Install

Download the installer or portable ZIP from [Releases](https://github.com/Duffed/apo-serein/releases/latest). Serein runs on Windows x64 (Windows 10 22H2 or later) and includes the .NET runtime.

Install [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) separately and enable it for your output device. Close Peace, then open **APO verbinden / verwalten** in Serein to connect.

## Build

Run `./build.ps1` on Windows with the .NET 10 SDK. Serein is licensed under [GPL-2.0-or-later](LICENSE.txt).
