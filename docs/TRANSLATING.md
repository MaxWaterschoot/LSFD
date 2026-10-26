# Translating LSFD

LSFD ships in English and Dutch. Every text the player sees comes from a language file in `Plugins/LSFD/Languages/`,
so you can add your own language without touching the code.

## Add a language

1. **Copy the template.** Copy `Plugins/LSFD/Languages/template.xml` to a file named after the language code, for
   example `de.xml` for German or `fr.xml` for French.
2. **Fill in the header.**

   ```xml
   <Language Name="Deutsch" Culture="de-DE" Translators="Your name">
   ```

   `Culture` sets how numbers and percentages are written: `de-DE` shows 1.240 L, `en-US` shows 1,240 L.
3. **Translate the texts.** Write the translation between the tags. The `en` attribute is the key, so leave it as it is.

   ```xml
   <String en="Water tank">Wassertank</String>
   <String en="~y~{0}~s~ is returning to quarters." note="{0} = unit.Definition.Name">~y~{0}~s~ fährt zurück zur Wache.</String>
   ```

   A text you leave empty is shown in English. You can release a half-finished translation and fill it in later.
4. **Try it.** Set `Language=de` under `[General]` in `LSFD.ini`, or type `LSFD_Language de` in the console.
   The console switches notifications and help texts right away; menus and tablet tabs change after reloading LSFD.

## Rules

- **Placeholders:** keep `{0}`, `{1}` and so on, in any order your language needs. The `note` attribute says what each
  one is. A format after a colon stays the same: `{0:N0}`, `{0:P0}`.
- **Colour codes:** keep the `~r~`, `~y~`, `~b~`, `~g~`, `~o~` codes around the same words, and `~s~` where the colour
  ends. `~n~` is a new line.
- **Prompts:** texts starting with a verb in lower case, such as `open the ~y~{0}~s~ compartments`, follow the frame
  "Press E to …" and "Hold E to …". Translate them so they fit your translation of that frame. In Dutch the frame is
  "Druk op E om …", so the prompt becomes `de vakken van de ~y~{0}~s~ te openen`.
- **Contexts:** a key with a bar, such as `her|possessive`, is a word that needs telling apart in some languages.
  Translate only the word; the part after the bar is a hint.
- **Pronouns:** callers say things like "{0} was screaming for help". `{0}` is the translation of `he` or `she` with a
  capital letter, `{1}` is often `him` or `her`. The note says which.

## Find what is missing

- **During play:** `LSFD_ExportMissingTranslations` writes `Languages/missing_xx.xml` with every text that was shown
  in English this session. Copy its entries into your file.
- **After an LSFD update:** run `python tools/i18n/extract_strings.py xx` from the repository. It adds the new texts to
  `xx.xml` with an empty translation, keeps everything you translated and drops texts LSFD no longer uses. A text whose
  English changed counts as new.

## Callout packs and XML callouts

- **XML callouts:** their message, advisory, help texts, task texts and caller lines go through the language file too,
  with the English text as the key.
- **Callout pack DLLs:** pack authors wrap their texts in `Language.T("...")` or `Language.F($"...")`, see
  [CALLOUTS.md](CALLOUTS.md#translations).
- **Shipping pack translations:** put them in a file of their own next to the main one, for example `nl.MyPack.xml`.
  LSFD loads it together with `nl.xml`.

Send finished translations to the beta Discord or the LCPDFR.com page, so they can ship with the next release.
