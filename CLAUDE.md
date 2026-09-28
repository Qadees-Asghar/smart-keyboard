# Working on SmartKeyboard

## Build, test, run
- `dotnet build SmartKeyboard.sln --no-incremental` must give 0 warnings; `dotnet test SmartKeyboard.sln` must be all green.
- The two benchmark tests (`AutocorrectBenchmarkTests`, `RealWordBenchmarkTests`) are regression guards set to the last measured numbers. When a change really improves them, update the constants and the comment that records the numbers.
- `run.cmd` is for development (tray only, `--background`). `install.cmd [folder]` installs or updates the real app (default `D:\tools\SmartKeyboard`). Both close a running copy through `tools\close_running.cmd`.
- Close the app by setting the named event `SmartKeyboard.ExitRequested.v1`. `taskkill` without `/F` reports SUCCESS but never closes it, because its only window is hidden.

## Testing in real apps (System Wide Mode)
- Use the Store version of Notepad on Windows 11 as the reference app. Read results back through UI Automation (`TextPattern.DocumentRange.GetText`).
- Type with `SendInput` and real virtual-key codes, one key at a time. Do **not** use `WScript.Shell.SendKeys`: it toggled Caps Lock on during testing, and it types its own text in bursts that Notepad drops.
- Before typing test text, set `learning=False` in `%APPDATA%\SmartKeyboard\settings.txt` and restart the app, then restore the file afterwards. Learning saves every typed pair, heavily weighted, and test sentences pollute the user's real data.
- `%APPDATA%\SmartKeyboard\inject.log` (setting `diagnostics=True`) records counts only. `sent=N/N` means Windows accepted every key; wrong text after that means the app dropped or reordered them.

## Rules the code depends on
- Notepad scrambles bursts of injected keys. `TextInjector` spaces key presses `KeyGapMs` (15 ms) apart on purpose; do not go back to one big `SendInput` call. The user's own keys are kept out of the middle by `KeyHoldGate`, not by batching.
- Anything that replaces text must be inside `Gate.Begin()` ... `finally Gate.End(ReplayHeld)`.
- Never replace a word whose start was not seen (`IsWordStartKnown` / `StartWasKnown`) unless `CaretTextCheck` has confirmed it from the app's text.
- `bigrams.txt` has no apostrophes, so context can never vouch for a contraction.
- Learning writes into the live `BigramIndex`, so anything judging "is this pair normal" must subtract `LearningEngine.GetLearnedPairCount`.
- The app project has `UseWPF=true` only for `System.Windows.Automation`, which drops the implicit `System.IO` using. It is added back in the csproj.
- Window-message broadcasts do not reach the hidden tray window. Use `InstanceSignal` (named events) for talking between copies.

## Editing files
- `.cmd` files must keep CRLF line endings (`.gitattributes` enforces it on commit).
- When editing with Python, use raw strings for Windows paths. `"\b"` and `"\n"` inside a path have silently corrupted files here before.
- Commit on a short feature branch, fast-forward `main`, push, and delete the branch. Only commit when asked.
