"""Download the three credited CC0 music files. Uses the supplied HTTP proxy, if any."""
from pathlib import Path
import argparse
import hashlib
import json
import urllib.request

TRACKS = [
    ('menu.mp3', 'Medieval: The Old Tower Inn', 'RandomMind', 'medieval-the-old-tower-inn', 'The_Old_Tower_Inn.mp3'),
    ('battle.ogg', 'Heartfelt Battle', 'request', 'heartfelt-battle-loopable-fantasy-stringspianohorn', 'heartfelt-battle_loop.ogg'),
    ('boss.ogg', 'Fairy Battles', 'MintoDog', 'fairy-battles', 'fairy_battles_bpm180_0.ogg'),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--proxy', help='For example http://127.0.0.1:7897')
    args = parser.parse_args()
    handlers = [urllib.request.ProxyHandler({'http': args.proxy, 'https': args.proxy})] if args.proxy else []
    client = urllib.request.build_opener(*handlers)
    root = Path(__file__).resolve().parents[1] / 'Assets/Resources/FortressAudio/Music'
    root.mkdir(parents=True, exist_ok=True)
    records = []
    for name, title, artist, slug, filename in TRACKS:
        source = 'https://opengameart.org/content/' + slug
        page = client.open(source, timeout=30).read().decode()
        if 'creativecommons.org/publicdomain/zero' not in page:
            raise RuntimeError('Verify the source license before downloading: ' + source)
        url = 'https://opengameart.org/sites/default/files/' + filename
        data = client.open(url, timeout=60).read()
        if name.endswith('.ogg') and not data.startswith(b'OggS'):
            raise RuntimeError('Unexpected audio response: ' + url)
        if name.endswith('.mp3') and not (data.startswith(b'ID3') or data[0] == 255):
            raise RuntimeError('Unexpected audio response: ' + url)
        (root / name).write_bytes(data)
        records.append(dict(file=name, title=title, artist=artist, source=source, download=url,
                            license='CC0 1.0', sha256=hashlib.sha256(data).hexdigest()))
        print(name, len(data), 'bytes')
    (root / 'Sources.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
