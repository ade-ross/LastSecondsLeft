<p align="center"><img src="docs/logo.png" width="96" alt="LastSecondsLeft logo"></p>

<h1 align="center">LastSecondsLeft</h1>

<p align="center">A tiny Windows tray app with two countdowns: one before your laptop battery dies, and one to rest your eyes.</p>

![The six countdown styles](docs/styles.png)

## Why

The low-battery notification is easy to ignore, because you know there's "still some time". LastSecondsLeft makes that time visible. When the laptop is **not charging** and the battery hits **5%**, a big animated **30 → 0** countdown floats on top of everything. At 0 the PC **hibernates**, so your open work is saved. Plug in the charger and the countdown disappears.

The same idea works for your eyes: after 20 minutes of screen time, a short, calm countdown reminds you to look away for 20 seconds. Both features live in one tray icon and can be set up (or turned off) in one settings panel. See [Eye breaks](#eye-breaks).

## Features

- 6 animated countdown styles: Ring, Orbit, Pulse, Spinner, Dots, Classic
- All in red, with a background that turns from near-black to deep red as time runs out
- Choose the start level (1–50 %), the length (5–600 s) and the size (50–95 % of the screen height)
- Never steals keyboard focus, so you can keep typing while it counts down
- Plugging in the charger cancels it; there's no snooze button and Alt+F4 doesn't close it
- Hibernates at 0, or does a normal shutdown if hibernation is disabled
- Very light: while idle it only checks the battery every 10 s, and the animation runs only while a countdown is on screen
- Optional **eye breaks** (20-20-20 rule): after 20 minutes of screen time, a calm reminder to look away
- A single small `.exe` with no dependencies, built with the C# compiler that ships with Windows

## Install

1. Download this repository (**Code → Download ZIP**) and extract it.
2. Open the folder in **File Explorer** and double-click **`install.bat`**.

If there's no `LastSecondsLeft.exe` yet, `install.bat` builds it from the source first, so nothing else needs to be installed. The installer:
- copies the app to `%LOCALAPPDATA%\LastSecondsLeft`
- starts it now and at every login (current user only, no admin rights needed)

> **Windows SmartScreen:** the app isn't code-signed, so Windows may show a blue "Windows protected your PC" warning. Click **More info → Run anyway**. You can read the whole source in `LastSecondsLeft.cs`.

## Use

Find the logo in the tray (click `^` near the clock).

- **Double-click** it, or right-click it → **Settings…**, to pick the style, start level, length and size, and to set up eye breaks. **Preview** shows the selected style.
- Right-click → **Test countdown (safe)** shows a countdown that never hibernates. A test countdown closes with a click.

Settings are saved in `%LOCALAPPDATA%\LastSecondsLeft\settings.ini`.

## Styles

| Style | Look |
|---|---|
| Ring | a thick ring that empties as time runs out, plus an inner ring that fills once per second |
| Orbit | four rings: time left with a glowing head, a per-second sweep, rotating dashes, counter-rotating arcs |
| Pulse | a heart that beats every second and sends out ripples |
| Spinner | 12 dots with a loading-style comet going round once per second; dots go dark as time runs out |
| Dots | 10 dots that light up one by one every second, plus 60 tiny dots for the time left |
| Classic | a simple red box |

## Eye breaks

<img src="docs/eyebreak.png" width="460" alt="Eye break reminder">

Long stretches of looking at a screen can tire your eyes. A common recommendation is the **20-20-20 rule**: every 20 minutes, look at something about 20 feet (6 m) away for 20 seconds. Eye-care organizations widely recommend it; studies show it can ease eye-strain symptoms, although the evidence for the exact numbers is limited.

- Only **screen time** counts: if you don't touch the mouse or keyboard for 5 minutes, or the PC sleeps, the timer starts over (your eyes were resting anyway).
- The reminder is deliberately calm and simple, so there's nothing to stare at. It closes by itself, and a soft sound tells you when to look back.
- **Skip** it when you're in a meeting or presenting. If Skip is too tempting, hide the button: then the only way to end a break early is to turn eye breaks off in Settings. The battery countdown always takes priority.
- Settings: on/off, every 5–120 minutes, 10–300 seconds, window size (20–95 % of the screen width, default 40), Skip button on/off, sound on/off. The tray menu shows when the next break is due, and **Take an eye break now** starts one right away.
- Limitation: watching a video without touching anything for 5+ minutes counts as "away".

This is a habit helper, not medical advice. If your vision changes, see an eye doctor.

## Uninstall

Double-click **`uninstall.bat`**. It stops the app, removes it from startup and deletes its folder and settings.

## Build from source

Needs Windows 10 or 11, which already includes .NET Framework 4.x and its C# compiler.

```bat
build.bat
```

This creates `LastSecondsLeft.exe`. The logo is drawn in code: on the first build the app writes `LastSecondsLeft.ico` itself, and the exe is then rebuilt with it.

For development, the exe also takes these arguments:

| Argument | What it does |
|---|---|
| `/demo[:Style] [/seconds:N] [/size:N]` | shows one test countdown, then exits |
| `/demo:Eye [/seconds:N] [/size:N]` | shows one eye break, then exits |
| `/writeicon:<path>` | saves the logo as a multi-size `.ico` |
| `/snapshots:<folder>` | renders `logo.png`, `styles.png` and `eyebreak.png` (the images in this README) without opening any window |

## Files

| File | What |
|---|---|
| `LastSecondsLeft.cs` | all the source code |
| `LastSecondsLeft.ico` | the logo, generated by the app itself |
| `build.bat` | builds the exe |
| `install.bat` / `uninstall.bat` | install / remove |
| `docs/` | README images |

## License

[MIT](LICENSE)
