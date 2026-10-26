# Beta testing

Thanks for testing LSFD. A beta finds the bugs that never show up on the developer's PC: other graphics cards, other
game versions and, above all, other mod combinations.

## Before you start

- **Use a clean install.** Install LSFD over a clean `Plugins/LSFD/` folder. Keep your `Profiles/` folder if you
  want to keep your career.
- **Check your versions.** You need an up-to-date RAGE Plugin Hook and RAGENativeUI. Note your other big mods, such as
  LSPDFR, EUP, ELS, map mods and vehicle packs.

## Reporting a bug

1. **Make the report file.** Right after the bug happens, open the RPH console (F4) and type `LSFD_BugReport`. It
   writes `Plugins/LSFD/BugReport.txt` with:
   - LSFD, RPH and game versions
   - the names of your mods and plugins
   - your `LSFD.ini`
   - what LSFD was doing
   - the end of `RagePluginHook.log`

   It lists file names only, nothing personal.
2. **Post it in the bug channel** with:
   - **What you did**, step by step. "I accepted a house fire, drove there, put my mask on and the game froze" beats
     "it crashed".
   - **What you expected** and what happened instead.
   - **Whether you can make it happen again.** Bugs that happen every time get fixed first.
   - **The bug report file** as an attachment. Add a screenshot or a short clip when it is about something you see.
3. **One bug per report.** That way each one can be tracked and closed on its own.

## Crashes

When the game or the plugin crashes, `LSFD_BugReport` may not work anymore. Attach `RagePluginHook.log` from your GTA V
folder instead; it is overwritten the next time you start the game, so save a copy first.

## Useful console commands

| Command | |
| --- | --- |
| `LSFD_Dispatch "House Fire"` | Starts a specific call to test it again |
| `LSFD_AmbientIncident` | An incident on the road ahead |
| `LSFD_OtherUnitsIncident` | Other units go to a call |
| `LSFD_Language nl` | Switch the language |
| `LSFD_ExportMissingTranslations` | Lists texts that are still in English |
| `LSFD_BugReport` | Writes the bug report file |
