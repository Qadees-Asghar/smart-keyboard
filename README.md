# SmartKeyboard

A Windows keyboard assistant that completes words, fixes typos, predicts the
next word, and learns from what you actually type. It works inside its own
editor and system wide, in any app on the PC.

This is a semester project for Software Engineering and Data Structures and
Algorithms. The point of it is the data structures: the Trie, the Max Heap,
the BK Tree and the Levenshtein distance are all written by hand. There is no
`PriorityQueue`, no library doing the work, and no machine learning anywhere.
"Learning" is counting.

## What it does

- Suggests words from a dictionary of **86,000 words** as you type
- Ranks them by how common they are and by the word before them
- Fixes typos the moment you press space: `teh` to `the`, `cant` to `can't`
- Predicts the next word from **269,000 word pairs**
- Underlines words it does not know, with fixes on right click
- Learns your habits, stored as counts only and never as text
- Lets you add your own words

Two ways to use it:

- **Editor Mode**, a text editor window with the suggestions built in
- **System Wide Mode**, a tray program that works in any app, with a popup
  that never steals focus

System Wide Mode tries hard to stay out of the way:

- it **switches off in code editors and terminals**, because code is not
  English and the popup would take Enter and the arrow keys away from the keys
  an editor needs most
- it stops completely whenever a **password box** has focus
- it only appears once **two letters** of a word are typed, and only when
  something that takes text has focus, so a key pressed on the desktop does
  nothing
- it **disappears on its own** a couple of seconds after you stop typing
- while a word is being fixed, the next letters you type are held for about a
  tenth of a second and then put in, so your typing can never land in the
  middle of a correction
- **a word is only ever replaced if SmartKeyboard watched it from its first
  letter.** It cannot read the other app, so it follows what you type key by
  key. Anything that loses that thread, a click, an arrow key, switching
  window, means the letters it counted may only be the tail of a longer word,
  and replacing on that count would corrupt the line. So it stays quiet for
  that one word and picks up again at the next space

One limit that cannot be fixed from here: inside a browser there is no way to
tell a text box from the rest of the page. Chromium reports no caret and gives
one window class for everything, so a stray letter pressed in a browser with
no text box focused can still bring the box up.

## Installing it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

Double click **`install.cmd`**. It builds SmartKeyboard into
`D:\tools\SmartKeyboard`, puts a **SmartKeyboard icon on the desktop and in
the Start menu**, sets it to **start with Windows**, and starts it. To install
somewhere else, run `install.cmd "E:\Apps\SmartKeyboard"`.

- **Double click the desktop icon** to open the SmartKeyboard window. It
  carries on working in other apps from the tray after the window is closed.
  Clicking the icon while it is already running brings the window back.
- **Start with Windows** starts it quietly in the tray when you sign in. Turn
  it on or off from the tray menu or from Settings.
- **To update**, run `install.cmd` again. It closes the running copy first,
  letting it save what it learned, and it keeps your Start with Windows choice.
- **To remove it**, run `uninstall.cmd`. It takes away the program, the icons
  and starting with Windows.

Your words, settings and everything SmartKeyboard learned live in
`%APPDATA%\SmartKeyboard`, never in the install folder, so updating or
uninstalling never touches them.

## Running it while working on the code

Double click **`run.cmd`**, or from this folder:

```
dotnet build -c Release
start src\SmartKeyboard.App\bin\Release\net10.0-windows\SmartKeyboard.App.exe --background
```

Started this way there is no window. SmartKeyboard lives in the **tray, by
the clock**. The icon appears straight away and says "Loading dictionary..." for
a moment while it reads the word list. Right click it for the editor,
settings and exit.

**Only one copy ever runs.** Start it again and the new one closes itself and
asks the running one to open its window instead. `run.cmd` goes further and
closes the running copy before it builds, asking it to save first, because
Windows locks the program file while it runs, so building on top of a running
copy would fail and leave you using the old one without saying so.

Use `start`, not `dotnet run`. With `dotnet run` the program belongs to that
terminal and closing it closes SmartKeyboard.

## Written by hand

| Structure | Used for |
|-----------|----------|
| Trie | finding every word that starts with what you typed |
| Max Heap | pulling the best few suggestions out of the matches |
| BK Tree | finding real words close to a typo, without checking all 86,000 |
| Levenshtein distance | measuring how far apart two words are |
| Bigram index | working out which word usually comes next |

## Layout

```
src/SmartKeyboard.Core    the data structures and the engine, no Windows in it
src/SmartKeyboard.App     both user interfaces and all the Windows code
tests/SmartKeyboard.Tests 635 tests
tools/                    builds the dictionary from public word lists
docs/DESIGN.md            how all of it works, and why
```

Core never references Windows Forms or the Windows API, so the engine can be
tested on its own and both modes share exactly the same code.

## Tests

```
dotnet test
```

635 tests. The BK Tree is checked against a brute force scan so a pruning bug
cannot hide, and autocorrect is measured against 4,061 misspellings from
Wikipedia's list of common misspellings.

## More

[docs/DESIGN.md](docs/DESIGN.md) covers the design decisions, the ranking
rules, how the dictionary is built and kept clean, the Windows hooks behind
System Wide Mode, and the limits that cannot be worked around.
