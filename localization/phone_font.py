"""Give the phone (Sidekick) font of the player's own game the Portuguese letters O and o with a tilde.

The font has no Õ/õ, but it has Ö/ö (0xD6/0xF6), which Portuguese doesn't use. Their glyphs are
redrawn as Õ/õ (the plain letter's diaeresis replaced by the tilde of Ã/ã) and their codes become
0xD5/0xF5, which keeps the code table sorted. Nothing changes size: an earlier version that added
glyphs grew the resource's virtual segment, and the whole phone screen came out corrupted.
"""
import argparse
import ctypes
import hashlib
import os
from pathlib import Path
import shutil
import struct
import sys
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_ptbr as bp
import mission_font as mf

TARGET = 1921         # the Sidekick phone resource (sidekick-font100-height800-000.dds)
FONT_OFFSET = 0x5300  # the font object in its system segment
# (letter given the tilde, the code it takes, the code it replaces, the letter the tilde comes from)
CHANGES = [('O', 0xd5, 0xd6, 'Ã', 'A'), ('o', 0xf5, 0xf6, 'ã', 'a')]


def patch(data, flags):
    system_size = (flags & 0x7ff) << (((flags >> 11) & 15) + 8)
    system = bytearray(data[:system_size])
    physical = bytearray(data[system_size:])

    def ptr(offset):
        return struct.unpack_from('>I', system, offset)[0] & 0xfffffff

    count = struct.unpack_from('>H', system, FONT_OFFSET + 160)[0]
    codes_at = ptr(FONT_OFFSET + 16)
    codes = list(struct.unpack_from('>%dH' % count, system, codes_at))
    table = struct.unpack_from('>I', system, FONT_OFFSET + 164)[0] & 0xfffffff
    glyph_ptr = ptr(table + 4)
    glyphs = [list(struct.unpack_from('>8f', system, glyph_ptr + i * 32)) for i in range(count)]
    assert struct.unpack_from('>H', system, table + 20)[0] == 1 and not ptr(table + 12)
    texture = ptr(ptr(table))
    fetch = ptr(texture + 28) + 28
    d0, d1, d2 = struct.unpack_from('>3I', system, fetch)
    assert d0 >> 31 == 1 and d1 & 63 == 19 and (d1 >> 6) & 3 == 1
    pitch = ((d0 >> 22) & 511) * 32 // 4
    base = (d1 & ~4095) - 0x60000000

    def pixel(x, y):
        offset = base + mf.tiled_offset(x // 4, y // 4, pitch)
        block = mf.swap16(physical[offset:offset + 16])
        alpha = int.from_bytes(block[:8], 'little')
        return (alpha >> (4 * ((y & 3) * 4 + (x & 3)))) & 15

    def write_pixel(x, y, value):
        # Only the pixel's alpha: the block's colour half stays as it was.
        offset = base + mf.tiled_offset(x // 4, y // 4, pitch)
        block = mf.swap16(physical[offset:offset + 16])
        alpha = int.from_bytes(block[:8], 'little')
        shift = 4 * ((y & 3) * 4 + (x & 3))
        alpha = (alpha & ~(15 << shift)) | (value << shift)
        block[:8] = alpha.to_bytes(8, 'little')
        physical[offset:offset + 16] = mf.swap16(block)

    for plain, new_code, old_code, accented, base_letter in CHANGES:
        target = codes.index(old_code)
        og, tg = glyphs[codes.index(ord(plain))], glyphs[target]
        ag, bg = glyphs[codes.index(ord(accented))], glyphs[codes.index(ord(base_letter))]
        w, h = int(tg[2]), int(tg[3])
        pad = h - int(og[3])           # rows above the letter in the glyph being redrawn
        tilde = int(ag[3]) - int(bg[3])  # rows above the letter in the accented one
        assert int(og[2]) == w and int(ag[2]) == w and 1 <= pad <= 12 and 1 <= tilde <= 12, (plain, og, tg, ag)
        tx, ty = int(tg[0]), int(tg[1])
        for yy in range(h):
            for xx in range(w):
                if yy < pad:
                    # The tilde: the accented glyph's top rows.
                    value = pixel(int(ag[0]) + xx, int(ag[1]) + yy)
                else:
                    value = pixel(int(og[0]) + xx, int(og[1]) + yy - pad)
                write_pixel(tx + xx, ty + yy, value)
        struct.pack_into('>H', system, codes_at + 2 * target, new_code)
        codes[target] = new_code
    assert codes == sorted(codes)
    return bytes(system + physical)


def update(game, runtime, dry_run=False):
    api = bp.helpers()
    image = api['decrypted_image'](game / 'default.xex', bp.ROOT / 'third_party/rexglue-sdk')
    key = image[0x7d8ac4:0x7d8ae4]
    archive = game / 'xarchive_cache.rpf'
    header, toc, entries = bp.read_index(archive, api['_aes'], key)
    entry = entries[TARGET]

    def read_resource(stream, e):
        stream.seek(e[2] & ~255)
        return stream.read(e[1])

    with os.add_dll_directory(str(runtime.resolve())):
        library = ctypes.CDLL(str((runtime / 'rexruntime.dll').resolve()))
        decompress = getattr(library, '?lzx_decompress@@YAHPEBX_KPEAX1I21@Z')
        decompress.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t,
                               ctypes.c_uint32, ctypes.c_void_p, ctypes.c_size_t]
        with archive.open('rb') as source:
            before = mf.unpack(read_resource(source, entry), decompress)
        system_size = (entry[3] & 0x7ff) << (((entry[3] >> 11) & 15) + 8)
        if b'sidekick-font100-height800-000.dds' not in before[:system_size]:
            raise ValueError('Unknown phone resource')
        count = struct.unpack_from('>H', before, FONT_OFFSET + 160)[0]
        codes_at = struct.unpack_from('>I', before, FONT_OFFSET + 16)[0] & 0xfffffff
        if 0xd5 in struct.unpack_from('>%dH' % count, before, codes_at):
            print('phone font: already has the tilde letters')
            return
        after = patch(before, entry[3])
        assert len(after) == len(before)
        print('phone font: patched, sha256', hashlib.sha256(after).hexdigest())
        if dry_run:
            return
        packed = mf.pack_lzx(after, entry[3])
        position = (archive.stat().st_size + 2047) & ~2047
        if position >= 0x80000000:
            raise ValueError('Phone font resource exceeds RPF3 offset limit')
        temporary = archive.with_suffix('.' + uuid.uuid4().hex + '.tmp')
        try:
            shutil.copyfile(archive, temporary)
            with temporary.open('r+b') as out:
                out.seek(position)
                out.write(packed)
                struct.pack_into('<4I', toc, TARGET * 16, entry[0], len(packed), position | 27, entry[3])
                disk_toc = bytes(toc)
                if header[4]:
                    for _ in range(16):
                        disk_toc = api['_encrypt'](key, disk_toc, 'ChainingModeECB')
                out.seek(0x800)
                out.write(disk_toc)
            _, _, verified = bp.read_index(temporary, api['_aes'], key)
            with temporary.open('rb') as stream:
                if (mf.unpack(read_resource(stream, verified[TARGET]), decompress) != after or
                        any(old != new for i, (old, new) in enumerate(zip(entries, verified)) if i != TARGET)):
                    raise ValueError('Phone font archive verification failed')
            temporary.replace(archive)
        finally:
            temporary.unlink(missing_ok=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', required=True, type=Path)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--dry-run', action='store_true')
    options = parser.parse_args()
    update(options.game, options.runtime, options.dry_run)
