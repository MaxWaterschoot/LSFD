"""Collects every translatable LSFD text into the language files.

Usage (from the repository root):
    python tools/i18n/extract_strings.py                 writes Plugins/LSFD/Languages/template.xml
    python tools/i18n/extract_strings.py nl de           also updates nl.xml and de.xml

Texts are found in the C# code (L.T("..."), L.F($"..."), Language.T/F) and in the XML callouts in
Plugins/LSFD/Callouts/. Updating a language file keeps its translations, adds new texts with an empty translation
and drops texts LSFD no longer uses. The English text is the key, so a changed English text needs translating again.
"""
import os, re, sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LANGUAGES = os.path.join(ROOT, "Plugins", "LSFD", "Languages")
SKIP_DIRS = {"bin", "obj", "tools", "Examples", ".git"}
CALL = re.compile(r'\b(?:L|Language)\.([TF])\(')
ESCAPES = {'n': '\n', 't': '\t', 'r': '\r', '"': '"', "'": "'", '\\': '\\', '0': '\0'}


def read_string(s, i):
    """s[i] == '"'. Returns (text, end) with escapes resolved."""
    out = []; i += 1
    while s[i] != '"':
        if s[i] == '\\':
            c = s[i + 1]
            if c == 'u':
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            out.append(ESCAPES.get(c, c)); i += 2; continue
        out.append(s[i]); i += 1
    return ''.join(out), i + 1


def skip_code(s, i, closers):
    """Skips C# code until one of `closers` at depth 0; returns its index."""
    depth = 0
    while True:
        c = s[i]
        if c == '"':
            _, i = read_string(s, i); continue
        if c == '$' and s[i + 1] == '"':
            _, _, i = read_interpolated(s, i); continue
        if c == "'":
            i += 1
            while s[i] != "'": i += 2 if s[i] == '\\' else 1
            i += 1; continue
        if c in '([{': depth += 1
        elif c in ')]}':
            if depth == 0 and c in closers: return i
            depth -= 1
        elif depth == 0 and c in closers: return i
        i += 1


def read_interpolated(s, i):
    """s[i:i+2] == '$"'. Returns (format, hole_expressions, end) like FormattableString.Format."""
    out = []; holes = []; i += 2
    while True:
        c = s[i]
        if c == '\\':
            n = s[i + 1]
            if n == 'u':
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            out.append(ESCAPES.get(n, n)); i += 2; continue
        if c == '"': return ''.join(out), holes, i + 1
        if c == '{' and s[i + 1] == '{': out.append('{{'); i += 2; continue
        if c == '}' and s[i + 1] == '}': out.append('}}'); i += 2; continue
        if c == '{':
            start = i + 1
            end = skip_code(s, start, ',:}')
            expr = s[start:end].strip()
            spec = ''
            if s[end] in ',:':
                close = s.index('}', end)
                spec = s[end:close]; end = close
            out.append('{%d%s}' % (len(holes), spec)); holes.append(expr); i = end + 1; continue
        out.append(c); i += 1


def from_code():
    found = []
    for folder, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in sorted(files):
            if not name.endswith('.cs') or name == 'L.cs': continue
            path = os.path.join(folder, name)
            s = open(path, encoding='utf-8-sig').read()
            where = os.path.relpath(path, ROOT).replace(os.sep, '/')
            for m in CALL.finditer(s):
                i = m.end()
                while s[i].isspace(): i += 1
                if m.group(1) == 'T' and s[i] == '"':
                    text, _ = read_string(s, i); found.append((text, None, where))
                elif m.group(1) == 'F' and s.startswith('$"', i):
                    fmt, holes, _ = read_interpolated(s, i)
                    note = ', '.join('{%d} = %s' % (n, h) for n, h in enumerate(holes))
                    found.append((fmt, note, where))
    return found


def from_xml_callouts():
    found = []
    folder = os.path.join(ROOT, 'Plugins', 'LSFD', 'Callouts')
    for name in sorted(os.listdir(folder)) if os.path.isdir(folder) else []:
        if not name.endswith('.xml'): continue
        where = 'Callouts/' + name
        for callout in ET.parse(os.path.join(folder, name)).getroot().iter('Callout'):
            message = callout.findtext('Message') or callout.get('Name')
            texts = [message, callout.findtext('Advisory'), callout.findtext('ArrivalHelp'), callout.findtext('CompletedMessage')]
            for task in callout.iter('Task'):
                texts += [task.get('Text'), task.get('DoneMessage')]
            for bystander in callout.iter('Bystander'):
                lines = [l.text for l in bystander.iter('Line')]
                if lines: texts += [bystander.get('Label', 'Caller')] + lines
            found += [(t.strip(), None, where) for t in texts if t and t.strip()]
    return found


def unique(entries):
    seen = {}; order = []
    for text, note, where in entries:
        if text not in seen:
            seen[text] = (note, where); order.append(text)
    return [(t, seen[t][0], seen[t][1]) for t in order]


def write(path, entries, name, culture, translators, translations):
    root = ET.Element('Language', Name=name, Culture=culture, Translators=translators)
    root.text = '\n  '
    previous = None
    for text, note, where in entries:
        if where != previous:
            root.append(ET.Comment(' ' + where + ' ')); previous = where
        attributes = {'en': text}
        if note: attributes['note'] = note
        element = ET.SubElement(root, 'String', attributes)
        element.text = translations.get(text, '')
    for i, child in enumerate(root):
        child.tail = '\n  ' if i < len(root) - 1 else '\n'
    with open(path, 'w', encoding='utf-8') as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n')
        f.write('<!-- LSFD language file. Translate the text between the tags, keep {0}, {1} and the ~colour~ codes.\n'
                '     See docs/TRANSLATING.md. Empty means the English text is shown. -->\n')
        f.write(ET.tostring(root, encoding='unicode'))
        f.write('\n')


def main():
    entries = unique(from_code() + from_xml_callouts())
    os.makedirs(LANGUAGES, exist_ok=True)
    write(os.path.join(LANGUAGES, 'template.xml'), entries, 'Language name', 'en-US', '', {})
    print(f'template.xml: {len(entries)} texts')

    keys = {e[0] for e in entries}
    for code in sys.argv[1:]:
        path = os.path.join(LANGUAGES, code + '.xml')
        name, culture, translators, translations = code, '', '', {}
        if os.path.exists(path):
            root = ET.parse(path).getroot()
            name, culture, translators = root.get('Name', code), root.get('Culture', ''), root.get('Translators', '')
            translations = {s.get('en'): (s.text or '') for s in root.iter('String')}
        dropped = [t for t in translations if t not in keys and translations[t].strip()]
        write(path, entries, name, culture, translators, translations)
        done = sum(1 for t in keys if translations.get(t, '').strip())
        print(f'{code}.xml: {done}/{len(entries)} translated' + (f', {len(dropped)} no longer used' if dropped else ''))


if __name__ == '__main__':
    main()
