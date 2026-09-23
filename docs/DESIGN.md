# SmartKeyboard: how it works

The full notes. See the [README](../README.md) for the short version.

A Windows desktop program that helps you type. It completes words, fixes typos,
predicts the next word, and learns from the words you actually use.

Everything is built with data structures and simple counting.
There is no AI and no machine learning.

## Two modes

1. Editor Mode: works inside the program's own text editor window.
2. System Wide Mode: works in any app on the PC while you type.

## Requirements

- Windows
- .NET 10 SDK

## Build and run

```
cd SmartKeyboard
dotnet build
dotnet test
dotnet run --project src/SmartKeyboard.App
```

Starting it normally puts a tray icon by the clock and begins watching for
words in every app. To open just the editor with no keyboard watching:

```
dotnet run --project src/SmartKeyboard.App -- --editor
```

## Try it

Start the app and type a few letters, for example `prog`.
A list of suggested words appears under the caret.

| Key       | What it does                          |
|-----------|---------------------------------------|
| Enter     | accept the highlighted word           |
| Tab       | accept it too, if you prefer Tab      |
| Up, Down  | move through the list                 |
| Esc       | hide the list                         |
| Click     | accept the word you clicked           |

Enter accepts a suggestion while the list is showing. Press Esc first if you
want Enter to start a new line instead.

Type a misspelled word such as `keybord` and it turns red once you pause.
Right click it to see up to 5 fixes, plus `Ignore` and `Add to Dictionary`.

### Next word prediction

Finish a word and press space. Before you type a single letter of the next
word, SmartKeyboard offers the five words most likely to follow. Type `good `
and it offers `morning`, `night`, `luck`. Press Enter to take the top one,
then space again, and you can build a sentence without typing much at all.

This comes from the 90,000 word pairs counted from real sentences:

    P(next | previous) = how often the pair appeared / how often the first word appeared

A full stop, question mark or exclamation mark clears the context, because
what came before the end of a sentence says nothing about what comes next.
If the previous word has never been seen with anything after it, the most
common words are offered instead.

### It learns from you

The shipped dictionary comes from ten old novels, so it thinks "good bye"
(65 uses) and "good night" (54) are far more likely than "good morning" (6).
Type `good morning` about three times and `morning` becomes the top
suggestion after `good`, and stays that way after a restart.

It only ever learns words it already knows, so typing a typo fifty times never
makes it a real word. Your counts go to `learned.txt`, and `words.txt` and
`bigrams.txt` are never written to.

`Tools` has `Learn from my typing` and `Reset what has been learned`.

### Suggestions use the word before

Suggestions are ranked by the previous word, not just by how common a word is:

    Score(w) = 0.7 x P(w | previous word) + 0.3 x P(w)

So after `of`, typing `th` puts `the` first, because `of the` is the commonest
word pair in English. With no previous word it falls back to popularity alone.

### Once you have typed a whole word

Almost every longer word that starts with a common short word is rarer, and
most of them are surnames. Typing `and` used to offer `andrew, anderson, andy,
andreas`, and typing `how` used to offer `however, howard, howe, howl`. Nobody
typing `and` wanted `anderson`.

So once what you have typed is already a word, a longer word is only offered
if it is at least a fifth as common. `how` keeps `however`, which is a real
thing to mean instead, and loses the surnames. `and`, `are`, `is` and `the`
lose everything, and an empty list simply closes the popup, which is right:
you had already typed what you meant.

A prefix that is not yet a word, like `hel` or `th`, is never filtered, because
there the whole point is to finish it for you.

### Autocorrect

Type `teh` then press space, and it becomes `the`. Press `Ctrl+Z` straight
after and your original word comes back.

Autocorrect only acts when it is nearly certain:

| Mistakes in the word | What it needs to act                                  |
|----------------------|-------------------------------------------------------|
| one                  | the winner is twice as common as the runner up        |
| two                  | 6 letters or longer, and four times as common         |
| three or more        | never fixed, only underlined in red                   |

If two real words are equally close, it changes nothing and leaves the red
underline to do the talking. It never touches words you added, anything with a
digit in it, or a Capitalised word in the middle of a sentence, because those
are usually names.

Between two words that are equally close, the more likely kind of slip wins,
whatever the counts say. Every key you pressed is evidence, so the kinds that
keep all of them come first, and changing a letter, which throws one away,
comes last:

| Kind of slip | Example | Beats |
|--------------|---------|-------|
| an apostrophe left out             | `cant` to `can't` | `canto`, used 6 times in the whole list |
| a double letter missed or repeated | `helo` to `hello` | `help`, 18 times commoner |
| two letters the wrong way round    | `aer` to `are`    | `aero`, which `aer` also starts |
| a word not finished                | `hav` to `have`   | `has` and `had` |
| a letter changed                   | last resort       | |

The apostrophe row is top because every letter was typed correctly and in the
right order, and it is the key people skip most, often on purpose. The counts
are no help there and usually point the wrong way: `your` is used 17826 times
against 159 for `you're`, and `that` 29392 times against 370 for `that's`.
Left to popularity, `youre` came out as `your` and `thats` as `that`.

That last row is why this matters. `hav` used to be left alone, because `have`,
`has` and `had` are all one edit away and no count can separate them. And `wor`
used to become **`for`**, which is fourteen times commoner than `work` and one
letter away. Only `have` and `work` keep the letters that were actually
pressed. The counts now only decide between two words of the same kind.

The same rule fixes `hous`, `goin`, `wher`, `becaus`, `peopl` and `somethin`.

While you are still typing, a contraction is offered too. The apostrophe sits
in the middle of `can't`, so walking the Trie from `cant` never reaches it and
wanders off down `canterbury, canton, cantonese` instead. It is looked up
separately and put at the top of the list.

### How well it actually works

Measured against **4061 misspellings from Wikipedia's list of common
misspellings**, which is somebody else's list, not one chosen to flatter this:

| | |
|---|---|
| corrected to exactly the right word | **87.9%** |
| right word somewhere in the top 5 fixes | **96.3%** |
| left alone and underlined instead | 6.2% |
| corrected to the wrong word | 5.9% |

And the number that matters more, because a wrong change is worse than no
change: **5191 correctly spelled words were run through autocorrect and none
of them was altered.**
Both `Autocorrect on space` and `Also fix messier words` can be switched off
under `Tools`.

Backspace never triggers autocorrect, so you can always fix a word by hand.

Some typos are genuinely ambiguous and are deliberately left alone. `hwo` is
one letter from both `who` (3287 uses) and `how` (2385), and neither is twice
the other, so guessing would be a coin flip. `aer` is closest to `her` (7643),
not `are` (6378), so "fixing" it would have made it worse. Both get a red
underline instead, and a right click shows the choices.

### Settings

`Tools` then `Settings`, or `Ctrl+,`. Everything is saved between runs.

| Option                                | What it does                          |
|---------------------------------------|---------------------------------------|
| Fix clear typos when I finish a word  | autocorrect on space                  |
| Also fix messier words                | allows two mistakes in long words     |
| Mark unknown words in red             | the red underline                     |
| Suggest the next word after a space   | next word prediction                  |
| How many suggestions to show          | 1 to 10, normally 5                   |
| Learn from my typing                  | adaptive learning                     |
| Fix typos in other apps too           | System Wide Mode, off by default      |

The options live in `%AppData%\SmartKeyboard\settings.txt` as plain text,
so you can read and edit them in Notepad. A line that makes no sense is
skipped and that option keeps its normal value, so a broken file never stops
the program from starting. Delete the file to go back to the defaults.

### Your own words

Open `Tools` then `My Dictionary`, or press `Ctrl+D`, to see, add, and remove
the words you taught SmartKeyboard. Type your own name, add it, and it stops
being red. Added words are saved at once and are still there after a restart.

The list stays up for as long as you are inside a word. It only goes away
when you press space or punctuation, press Esc, or click somewhere else.
The word you are typing is kept in the list, so Tab always has something
sensible to do: finish the word and add the space.

The bar at the bottom shows how many words are loaded and how long the last
suggestion took, so you can see the speed for yourself.

## System Wide Mode

Start SmartKeyboard and it sits in the tray by the clock. Open Notepad, a
browser, or a chat app, type a few letters of a word, and the suggestion popup
appears under your cursor. Use Up and Down to pick one, and Enter to take it.

| Key or action     | What it does                                   |
|-------------------|------------------------------------------------|
| Enter             | accept the highlighted word                    |
| Click a word      | accept that word                               |
| Up, Down          | move through the list                          |
| Esc               | close the popup                                |
| Ctrl+Alt+K        | pause or resume, from anywhere                 |
| Double click tray | open the editor                                |
| Right click tray  | pause, editor, settings, exit                  |

Enter is the only key that accepts, so there is nothing to memorise. Clicking
a word works as well, and it does not disturb the app you are typing into: the
popup answers `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`, so the click lands on
the popup while the caret stays exactly where it was.

Those keys are only taken while the popup is showing. With no popup up, Enter,
Up, Down and Esc behave exactly as the app underneath expects, so pressing
Enter still sends your message in a chat app.

Tab is never taken, even with the popup up. In other apps Tab moves to the
next field, and stealing that to insert a word would be a nasty surprise.

### It ignores its own typing

When you accept a suggestion, SmartKeyboard types it with `SendInput`. Those
keys travel through low level keyboard hooks exactly like real ones, including
its own, so without a way to tell them apart it reads its own output back and
loses track of the word.

Every key it sends carries a signature in `dwExtraInfo`, and the hook passes
anything with that signature straight through untouched.

### How it stays out of the way

The popup never takes focus. It uses `WS_EX_NOACTIVATE` and
`ShowWithoutActivation`, so the app you are typing into keeps the caret and
your next keystroke goes where you expect.

The keyboard hook sits inside the operating system's input path, ahead of the
app the key is going to, so it has to be quick. If it is slow, every app on
the PC feels slow, and Windows quietly removes a hook that takes too long. So
the hook callback only updates the word being typed and asks for a refresh.
The dictionary lookup happens on a background thread, and the popup is only
touched on the UI thread.

Typing resets when you click, press an arrow key, or switch windows, because
at that point SmartKeyboard no longer knows where the caret is. The window is
spotted by its handle, not its title, because browsers and chat apps change
their title while you type.

Clicks need a second hook, on the mouse, because a click moves the caret
without any key being pressed. Nothing would otherwise tell SmartKeyboard, and
it would carry on believing you were half way through a word that is no longer
in front of the caret. That callback throws away mouse movement in its first
line, since it sits in the path of every mouse event on the PC.

Accepting a word can never damage your text. The list on screen is stored
together with the exact prefix it was worked out for, in one object, and
accepting is refused outright if what you have typed has moved on since. Held
as two separate values they could disagree for a moment, and the replacement
would then delete the wrong number of letters and leave wreckage like `how`
turning into `hoa`.

### Privacy

Typed text is never written to a file. The word you are typing is held in
memory only and is thrown away as soon as the word is finished. The only thing
saved is word counts, the same as Editor Mode.

Watching stops completely when a password may be on screen. It checks the
focused control directly, which catches normal Windows password boxes, and it
checks the window title for words like `password`, `sign in`, `bitwarden` and
`incognito`, which is the only thing that works inside a browser. If it cannot
tell, it stops anyway.

Autocorrect in other apps is **off** by default. Changing words inside someone
else's app without being asked is not something to do by default, so you turn
it on yourself. Right click the tray icon and tick `Fix typos in other apps`,
or use Settings.

### Two things it cannot do

These are limits of Windows, not bugs, and the program handles both quietly:

1. **Apps running as Administrator are invisible to it.** Windows does not let
   a normal program watch the keys going to an elevated one, or type into it.
   To use SmartKeyboard with an admin app, start SmartKeyboard as
   Administrator too. If a word is ever refused, the tray says so once rather
   than failing in silence.
2. **Some apps hide their caret.** Browsers and Electron apps draw their own
   and report nothing to Windows. Every way of asking was tried against a
   real one: `GetGUIThreadInfo` returns an empty rectangle, the app has no
   IME input context, and accessibility answers with the bounds of the whole
   window. So instead the popup goes where the user **last clicked**, because
   clicking into the box is how they got there, and that point only moves
   when they click again. Before this it followed the mouse pointer, which
   meant it wandered off to wherever the hand happened to leave it.

## Look and feel

The app uses Anthropic's brand style: Poppins for menus, buttons and the
suggestion list, and Lora for the text you write, on the warm off white
background with the orange accent.

Neither font is installed on a normal Windows PC, so both ship inside
`src/SmartKeyboard.App/Assets/Fonts` and are loaded at startup. Nothing is
installed onto your machine. Both are Open Font License, and the licence files
sit next to them. If the fonts ever fail to load, the app falls back to Arial
and Georgia, and the status bar says so.

## Project layout

```
src/SmartKeyboard.Core    all the logic and data structures, no UI code
src/SmartKeyboard.App     the Windows Forms app (both modes)
tests/SmartKeyboard.Tests xUnit tests for Core
tools/                    the script that builds the dictionary files
```

Core is a plain class library. It does not reference Windows Forms or any
Windows API, so the same engine is used by both modes.

## Data structures written by hand

| Structure     | Where it is used                                   |
|---------------|----------------------------------------------------|
| Trie          | finding every word that starts with what you typed |
| Max Heap      | picking the top 5 suggestions by score             |
| BK Tree       | finding words close to a typo                      |
| Edit Distance | measuring how far a typo is from a real word       |

Hash maps and hash sets use the built in `Dictionary` and `HashSet`, which the
project rules allow.

## Where the dictionary comes from

`src/SmartKeyboard.App/Data/words.txt` holds about 86,000 words with a count,
and `bigrams.txt` holds about 269,000 word pairs with a count.

Both files are built by `tools/generate_dictionary.py` from five free sources:

| Source              | What it gives                                       |
|---------------------|-----------------------------------------------------|
| `count_1w.txt`      | 333,000 words with real counts from a huge web corpus |
| `words_alpha`       | 370,000 words that are really in an English dictionary |
| `count_2w.txt`      | 286,000 word pairs with counts, for prediction      |
| Project Gutenberg   | the words with apostrophes, which the others drop   |
| Wikipedia           | a curated human list of 4000 common misspellings    |

The web list has the counts but is full of junk. The dictionary list says
which words are real but has no counts. Using both together gives real words
with real frequencies.

You only need to run the script again if you want to rebuild the data:

```
python tools/generate_dictionary.py
```

To use a different dictionary, just replace `words.txt` with any file that has
one `word count` per line. No code has to change.

### Keeping junk out of the word list

A spell checker is only as good as its word list. If rubbish gets in, real
typos stop being flagged. The web list is full of fragments (`ot`, `ts`, `nt`,
`ll`), contractions with the apostrophe dropped (`im`, `dont`, `whats`), and
misspellings. So the generator filters by length:

| Word length | Rule to get in                                        |
|-------------|-------------------------------------------------------|
| 1 letter    | only `a` and `i`                                      |
| 2 letters   | a fixed list of real two letter words                 |
| 3 letters   | must be used at least 10 times                        |
| 4 or more   | must be in the real English dictionary list           |

That three letter floor was 50, and it was throwing away real English.
Measured against the word list: of 252 everyday three letter words, a floor of
50 kept 201 and a floor of 10 keeps 246. `ill`, `hen`, `owl`, `hug`, `jaw`,
`pea` and forty others were missing, so the spell checker underlined them. The
cost is about six hundred more three letter entries, roughly half of them
abbreviations nobody types, and that is the better trade: junk sitting unused
is invisible, a missing real word puts a red line under correct writing.

`ill` was missing for a different reason. It was listed as the apostrophe free
spelling of `I'll`, which forgot that it is an ordinary word as well. The same
call is made for `its` and `were`: when a word is real in its own right it
stays, and autocorrect leaves it alone, because there is no way to tell which
was meant.

The interesting one is misspellings. A count floor cannot catch them, because
real but uncommon words have counts just as low: `quandary` is used twice and
`meticulous` five times, the same as the misspellings `enviroment` and
`accomodate`.

What separates them is the neighbours. A rare word that sits **one typo away
from a much commoner word** is almost certainly that typo. `enviroment`
appears 3 times right next to `environment` at 881, so it goes. `quandary` has
no common word one edit away, so it stays. That rule drops about 8,800
misspellings without losing a single one of the rare real words tested.

It cannot be pushed any harder, and that was measured rather than assumed.
The rule only looks at words of five letters or more. Extending it to four
would have deleted `deft`, `dour`, `ewer`, `fret`, `hone`, `laud`, `meld`,
`pare`, `sate`, `wane` and `yore` in order to catch nine misspellings, which
is roughly two hundred real words destroyed per typo caught. Four letter
English is too dense: nearly every four letter word is one edit from a
commoner one.

So the last hundred are removed using a **curated human list** instead,
Wikipedia's list of common misspellings. A word is only dropped when the right
spelling is in the dictionary too, the misspelling is rare here, and the right
spelling is the commoner of the two. That last condition is what lets `thru`
stay: it is on the list as a misspelling of `through`, but people write it on
purpose and it is common enough here to prove it.

`tests/SmartKeyboard.Tests/DictionaryQualityTests.cs` checks all of this, so
the junk cannot quietly come back if the data is rebuilt.

### Words people actually type

The counts come from web text, where chat words barely appear. Without help
the program underlined `bro` and `wassup` in red, offered nothing when you
started typing them, and would have autocorrected them into something else.

So about 65 of them are added by hand: `bro`, `wassup`, `lol`, `omg`, `btw`,
`pls`, `yep`, `nah`, `kinda`, `lemme`, `y'all`, `selfie`, `emoji`, `meme`,
`screenshot`, `hashtag` and the rest. Their counts are set deliberately
modest, high enough to be suggested and left alone by the spell checker, low
enough never to outrank ordinary English.

Nothing shorter than three letters was added, on purpose. A two letter word
turns up as a close match for almost every other short typo, and that costs
more than it gives.

Contractions were already there from the books: `can't`, `i've`, `don't`,
`you're`, `didn't`, `that's`, `let's`, `o'clock`. The apostrophe counts as a
letter, so `can't` is one word and not `can` followed by `t`. `y'all` was the
only one missing.

The list lives in `INFORMAL_WORDS` in `tools/generate_dictionary.py`, so
rebuilding the dictionary keeps them, and `DictionaryQualityTests` checks they
are still there.

## One change to the spec, and why

The spec says short words (4 letters or less) only look 1 edit away.
Plain edit distance counts swapping two letters as 2 edits, so `teh` would
never find `the`, which is the most common typo there is.

So SmartKeyboard counts a swap of two neighbouring letters as ONE mistake.
Swaps are checked straight against the Trie, which means the BK tree keeps
using real Levenshtein distance and its branch skipping stays correct.

The doubled letter rule above is the same idea from the other side. It does
not change how far the search looks, only which of the words it found is
picked, because plain edit distance treats every kind of slip as equally
likely and real fingers do not.

## Where your own files are saved

Your added words and learned counts go to
`%AppData%\SmartKeyboard\`, so rebuilding the project never deletes them.
The shipped dictionary files are never written to.

## Opening the code

Open **`SmartKeyboard.sln`** in Visual Studio. That is the whole solution:
opening a single `.csproj` shows you one project with its references missing,
which looks like half the code has vanished.

It needs the **.NET 10 SDK**, which is what the projects target. An older
Visual Studio will load the solution but refuse the projects, because it does
not know `net10.0`. Visual Studio Code works too: open the folder, not a file,
and install the C# extension.

The map:

| Where | What is in it |
|-------|---------------|
| `src/SmartKeyboard.Core/DataStructures/` | Trie, MaxHeap, BKTree, EditDistance, BigramIndex |
| `src/SmartKeyboard.Core/Engine/`         | suggestions, ranking, fuzzy matching, autocorrect, prediction, learning |
| `src/SmartKeyboard.Core/Data/`           | reading the word files, the Singleton loader |
| `src/SmartKeyboard.App/Editor/`          | the editor window, spell check painting, settings |
| `src/SmartKeyboard.App/SystemWide/`      | keyboard hook, mouse hook, popup, text injection, tray |
| `src/SmartKeyboard.App/Data/`            | words.txt and bigrams.txt |
| `tests/SmartKeyboard.Tests/`             | 411 tests |
| `tools/generate_dictionary.py`           | builds the two data files from scratch |

Core is where the hand written data structures live and it never touches
Windows or Windows Forms. App is everything that does.

## Running it

Double click **`run.cmd`** in this folder. It builds, starts the program, and
lets go of it, so you can close the black window straight away.

Or by hand, from this folder:

```
dotnet build
start src/SmartKeyboard.App/bin/Debug/net10.0-windows/SmartKeyboard.App.exe
```

`dotnet run` works too, but the program then belongs to that terminal and
closing it closes SmartKeyboard. `start`, or `run.cmd`, does not.

There is no window when it starts. SmartKeyboard lives in the **tray, by the
clock**, as a small orange keyboard icon. Right click it for the editor,
settings and exit, or double click it to open the editor. Closing the editor
does not close the program. **Exit** on the tray menu does, and it saves what
was learned on the way out.

## Progress

- [x] Step 1: solution and projects
- [x] Step 2: Trie and TrieNode (F2)
- [x] Step 3: Max Heap and top 5 ranking (F3)
- [x] Step 4: large dictionary loading (F1)
- [x] Early: working editor with live suggestions
- [x] Step 5: fuzzy typo correction (F4)
- [x] Step 6: user dictionary (F8)
- [x] Step 7: autocorrect (F5)
- [x] Step 8: next word prediction (F6)
- [x] Step 9: contextual suggestions (F7)
- [x] Step 10: adaptive learning (F9)
- [x] Step 11: rest of the Editor Mode UI (settings window)
- [x] Step 12: System Wide Mode (F10)
