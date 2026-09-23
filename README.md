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

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

Double click **`run.cmd`**, or from this folder:

```
dotnet build
start src\SmartKeyboard.App\bin\Debug\net10.0-windows\SmartKeyboard.App.exe
```

There is no window at startup. SmartKeyboard lives in the **tray, by the
clock**. Right click it for the editor, settings and exit.

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
tests/SmartKeyboard.Tests 520 tests
tools/                    builds the dictionary from public word lists
docs/DESIGN.md            how all of it works, and why
```

Core never references Windows Forms or the Windows API, so the engine can be
tested on its own and both modes share exactly the same code.

## Tests

```
dotnet test
```

520 tests. The BK Tree is checked against a brute force scan so a pruning bug
cannot hide, and autocorrect is measured against 4,061 misspellings from
Wikipedia's list of common misspellings.

## More

[docs/DESIGN.md](docs/DESIGN.md) covers the design decisions, the ranking
rules, how the dictionary is built and kept clean, the Windows hooks behind
System Wide Mode, and the limits that cannot be worked around.
