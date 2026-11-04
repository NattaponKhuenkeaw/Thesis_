"""Read original PNG palettes without dependencies; build a review-only palette draft.

Run from any directory. Does not write to Assets or alter input images.
"""

import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path
import struct
import zlib

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SPRITES = ROOT / 'Assets/Project/Character/Hero/test_15_goenitz/Goenitz sprite'


def read_png(path):
    data = Path(path).read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'Not PNG: {path}')
    pos, compressed, palette, alpha = 8, bytearray(), [], b''
    while pos < len(data):
        length = struct.unpack_from('>I', data, pos)[0]
        kind, chunk = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        if kind == b'IHDR':
            width, height, depth, mode, compression, filtering, interlace = struct.unpack('>IIBBBBB', chunk)
            if depth != 8 or mode not in (2, 3, 6) or interlace or compression or filtering:
                raise ValueError(f'Unsupported PNG format: {path}')
        elif kind == b'PLTE':
            palette = [tuple(chunk[i:i + 3]) for i in range(0, len(chunk), 3)]
        elif kind == b'tRNS':
            alpha = chunk
        elif kind == b'IDAT':
            compressed.extend(chunk)
        pos += length + 12
        if kind == b'IEND':
            break
    channels = {2: 3, 3: 1, 6: 4}[mode]
    stride = width * channels
    raw, previous, pixels, indices = zlib.decompress(compressed), bytearray(stride), [], []
    if len(raw) != height * (stride + 1):
        raise ValueError(f'Invalid scanline length: {path}')
    for y in range(height):
        start = y * (stride + 1)
        method = raw[start]
        row = bytearray(raw[start + 1:start + 1 + stride])
        for x in range(stride):
            left = row[x - channels] if x >= channels else 0
            up = previous[x]
            corner = previous[x - channels] if x >= channels else 0
            if method == 0:
                predictor = 0
            elif method == 1:
                predictor = left
            elif method == 2:
                predictor = up
            elif method == 3:
                predictor = (left + up) // 2
            elif method == 4:
                p = left + up - corner
                distances = (abs(p - left), abs(p - up), abs(p - corner))
                predictor = (left, up, corner)[distances.index(min(distances))]
            else:
                raise ValueError(f'Unknown filter: {method}')
            row[x] = (row[x] + predictor) & 255
        for x in range(0, stride, channels):
            if mode == 3:
                index = row[x]
                indices.append(index)
                pixels.append((*palette[index], alpha[index] if index < len(alpha) else 255))
            elif mode == 2:
                rgb = tuple(row[x:x + 3])
                transparent = bool(alpha) and rgb == struct.unpack('>HHH', alpha)
                pixels.append((*rgb, 0 if transparent else 255))
            else:
                pixels.append(tuple(row[x:x + 4]))
        previous = row
    return dict(width=width, height=height, palette=palette, pixels=pixels, indices=indices)


def hex_color(rgb):
    return '#' + ''.join(f'{c:02X}' for c in rgb[:3])


def inspect(references):
    files = sorted(SPRITES.glob('*.png'))
    palette_sets = collections.Counter()
    all_colors = collections.Counter()
    for path in files:
        image = read_png(path)
        counts = collections.Counter(image['pixels'])
        all_colors.update(counts)
        palette_sets[tuple(sorted(counts))] += 1
    print('Sprite files:', len(files), 'Used RGBA palette sets:', len(palette_sets), 'Used RGBA colors:', len(all_colors))
    idle = read_png(SPRITES / 'Goenitz_idle_0.png')
    counts = collections.Counter(idle['pixels'])
    print('Idle palette:', [(i, hex_color(rgb), counts[(*rgb, 255)]) for i, rgb in enumerate(idle['palette']) if counts[(*rgb, 255)]])
    print('All colors:', [(hex_color(rgb), rgb[3], n) for rgb, n in all_colors.most_common(75)])
    for ref in references:
        image = read_png(ref)
        print(Path(ref).name, [(hex_color(rgb), n) for rgb, n in collections.Counter(image['pixels']).most_common(55)])


def build(preview_path):
    normal_path = SPRITES / 'Goenitz_idle_0.png'
    red_path = SPRITES / 'Goenitz_limit-break_23.png'
    normal = read_png(normal_path)
    red = read_png(red_path)
    groups = {
        **dict.fromkeys((1, 2, 3), 'auxiliary highlights'),
        **dict.fromkeys((4, 5, 6), 'hair'),
        **dict.fromkeys((7, 8, 9, 10), 'skin'),
        **dict.fromkeys((12, 13, 15, 16, 17), 'clothing'),
        14: 'accent', 32: 'outline',
        **dict.fromkeys((39, 40, 41, 42), 'dark details (hood/boots)'),
    }
    entries = [dict(index=i, group=groups[i], source=hex_color(src), target=hex_color(dst),
                    evidence='Matching palette slot in original blue and red PNGs; appearance still needs review')
               for i, (src, dst) in enumerate(zip(normal['palette'], red['palette']))
               if i and src != dst]
    assert {entry['index'] for entry in entries} == set(groups)
    assert len({entry['source'] for entry in entries}) == len(entries)
    lookup = {entry['source']: entry['target'] for entry in entries}
    profiles = {
        'clothing': [12, 13, 15, 16, 17],
        'clothing-hair': [4, 5, 6, 12, 13, 15, 16, 17],
        'full-reference': [entry['index'] for entry in entries],
    }
    inventory, normal_counts, normal_files, red_files, effects = [], collections.Counter(), [], [], []
    samples, rgb_index_conflicts, cross_effect_matches = [], [], []
    palette_groups = collections.defaultdict(list)
    decoded = {}
    for path in sorted(SPRITES.glob('*.png')):
        im = read_png(path)
        decoded[path.name] = im
        palette_groups[tuple(im['palette'])].append(path.name)
        if im['palette'] == normal['palette']:
            classification = 'normal-character'
            normal_files.append(path.name)
            normal_counts.update(idx for idx, pixel in zip(im['indices'], im['pixels']) if pixel[3])
            if any(i not in groups and hex_color(im['palette'][i]) in lookup for i in set(im['indices'])):
                rgb_index_conflicts.append(path.name)
        elif im['palette'] == red['palette']:
            classification = 'existing-red-character'
            red_files.append(path.name)
        else:
            classification = 'other-palette-effects'
            effects.append(path.name)
            matches = sorted({hex_color(px) for px in im['pixels'] if px[3] and hex_color(px) in lookup})
            if matches:
                cross_effect_matches.append(dict(file=path.name, matching_source_colors=matches))
        counts = collections.Counter(hex_color(px) for px in im['pixels'] if px[3])
        inventory.append(dict(file=path.name, classification=classification,
                              width=im['width'], height=im['height'],
                              sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                              recolorable_pixels=sum(n for rgb, n in counts.items() if rgb in lookup)))
    for entry in entries:
        entry['normal_sprite_pixels'] = normal_counts[entry['index']]
        entry['normal_sprite_files'] = sum(entry['index'] in set(decoded[name]['indices']) for name in normal_files)
    requested = ['Goenitz_idle_0.png', 'Goenitz_idle_6.png', 'Goenitz_walk_0_0.png',
                 'Goenitz_attack1_0.png', 'Goenitz_airattack1_2.png',
                 'Goenitz_attack11_0.png', 'Goenitz_limit-break_17.png', 'Goenitz_limit-break_23.png']
    for prefix in ('Goenitz_hurt', 'Goenitz_jump', 'Goenitz_ultimate'):
        found = next((name for name in sorted(normal_files) if name.startswith(prefix)), None)
        if found:
            requested.append(found)
    for name in dict.fromkeys(requested):
        im = decoded[name]
        runs, previous, count = [], None, 0
        for index in im['indices']:
            if index == previous:
                count += 1
            else:
                if previous is not None:
                    runs.extend((previous, count))
                previous, count = index, 1
        runs.extend((previous, count))
        alpha = dict(zip(im['indices'], (px[3] for px in im['pixels'])))
        samples.append(dict(name=name, width=im['width'], height=im['height'], runs=runs,
                            palette=[[*rgb, alpha.get(i, 255)] for i, rgb in enumerate(im['palette'])],
                            isRed=name in red_files))
    mapping = dict(schema_version=1, status='draft', color_space='sRGB byte RGB',
                   source_sprite=str(normal_path.relative_to(ROOT)).replace('\\', '/'),
                   target_sprite=str(red_path.relative_to(ROOT)).replace('\\', '/'),
                   evidence='Original indexed PNG PLTE slots, not screenshot estimates',
                   match_rule='Simultaneous exact source RGB lookup; preserve input alpha; unmatched colors pass through',
                   profiles=profiles, entries=entries,
                   preserve='All transparent pixels and unmatched RGB; profile-excluded entries unchanged',
                   limitation='An RGB lookup cannot separate semantic regions sharing a color. Group labels are visual annotations, not segmentation masks.')
    audit = dict(sprite_pngs=len(inventory), normal_character_pngs=len(normal_files),
                 existing_red_character_pngs=len(red_files), other_palette_pngs=len(effects),
                 distinct_full_palettes=len(palette_groups), mapping_entries=len(entries),
                 normal_index_rgb_conflicts=rgb_index_conflicts,
                 effect_source_color_matches=cross_effect_matches,
                 palette_families=[dict(files=names, count=len(names)) for names in palette_groups.values()],
                 inventory=inventory)
    # Validate the complete lookup against every visible pixel of every normal-palette frame.
    checked = 0
    for name in normal_files:
        im = decoded[name]
        for index, pixel in zip(im['indices'], im['pixels']):
            if pixel[3]:
                assert lookup.get(hex_color(pixel), hex_color(pixel)) == hex_color(red['palette'][index]), (name, index)
                checked += 1
    assert not rgb_index_conflicts
    audit['validated_visible_normal_pixels'] = checked
    audit['checks'] = dict(all_normal_pixels_match_red_palette_slots=True,
                           alpha_and_geometry_preserved_by_preview=True,
                           note='Automated color checks do not establish semantic isolation, visual approval, or Unity rendering correctness')
    for filename, content in (('mapping.json', mapping), ('audit.json', audit)):
        (HERE / filename).write_text(json.dumps(content, indent=2) + '\n', encoding='utf-8')
    with (HERE / 'mapping.csv').open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(entries[0]))
        writer.writeheader()
        writer.writerows(entries)
    payload = json.dumps(dict(mapping=mapping, samples=samples), separators=(',', ':'))
    template = (HERE / 'palette-preview.template.html').read_text(encoding='utf-8')
    preview = Path(preview_path) if preview_path else HERE / 'palette-preview.html'
    preview.parent.mkdir(parents=True, exist_ok=True)
    preview.write_text(template.replace('__PALETTE_DATA__', payload), encoding='utf-8')
    print(json.dumps({k: v for k, v in audit.items() if k not in ('inventory', 'palette_families')}, indent=2))
    print('Preview:', preview)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inspect', nargs='*', metavar='REFERENCE_PNG')
    parser.add_argument('--preview', help='Optional destination for the generated preview fragment')
    args = parser.parse_args()
    if args.inspect is not None:
        inspect(args.inspect)
    else:
        build(args.preview)
