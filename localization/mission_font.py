"""Build the missing Portuguese glyphs from the player's own Complete Edition font."""
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

def tiled_offset(x, y, pitch):
    pitch = (pitch + 31) & ~31
    macro = ((x >> 5) + (y >> 5) * (pitch >> 5)) << 11
    micro = ((x & 7) + ((y & 14) << 2)) << 4
    offset = macro + ((micro & ~15) << 1) + (micro & 15) + ((y & 1) << 4)
    return ((offset & ~511) << 3) + ((y & 16) << 7) + ((offset & 448) << 2) + (((((y & 8) >> 2) + (x >> 3)) & 3) << 6) + (offset & 63)

def swap16(block):
    result = bytearray(block)
    result[0::2], result[1::2] = block[1::2], block[0::2]
    return result

def patch(data, shared, flags):
    data = bytearray(data)
    old_system_size = (flags & 0x7ff) << (((flags >> 11) & 15) + 8)
    system = bytearray(data[:old_system_size])
    physical = bytearray(data[old_system_size:])
    cursor = old_system_size
    def alloc(blob):
        nonlocal cursor
        cursor = (cursor + 15) & ~15
        offset = cursor
        cursor += len(blob)
        if len(system) < cursor:
            system.extend(bytes(cursor - len(system)))
        system[offset:cursor] = blob
        return 0x50000000 + offset
    def ptr(offset):
        return struct.unpack_from('>I', system, offset)[0] & 0xfffffff
    # The verified pause resource stores KP Duty JNL here (800px atlas).
    font_offset = 0x3d820
    count = struct.unpack_from('>H', system, font_offset + 160)[0]
    codes = list(struct.unpack_from(f'>{count}H', system, ptr(font_offset + 16)))
    widths_ptr = ptr(font_offset + 20)
    additions = [0xd5, 0xf5]
    assert all(c not in codes for c in additions)
    expanded = sorted(codes + additions)
    index_map = [codes.index(c) if c in codes else None for c in expanded]
    widths = [system[widths_ptr + i * 4:widths_ptr + i * 4 + 4] for i in range(len(codes))]
    widths_out = b''.join(widths[i] if i is not None else widths[codes.index(0x4f if c == 0xd5 else 0x6f)] for c, i in zip(expanded, index_map))
    struct.pack_into('>II', system, font_offset + 16,
                     alloc(struct.pack('>' + str(len(expanded)) + 'H', *expanded)), alloc(widths_out))
    struct.pack_into('>H', system, font_offset + 0xa0, len(expanded))
    for table in struct.unpack_from('>3I', system, font_offset + 164):
        if not table:
            continue
        table &= 0xfffffff
        glyph_ptr = ptr(table + 4)
        size = struct.unpack_from('>H', system, table + 8)[0]
        glyphs = [list(struct.unpack_from('>8f', system, glyph_ptr + i * 32)) for i in range(len(codes))]
        texture_table = ptr(table)
        texture_count = struct.unpack_from('>H', system, table + 20)[0]
        texture_indexes_ptr = ptr(table + 12)
        texture_indexes = list(system[texture_indexes_ptr:texture_indexes_ptr + len(codes)]) if texture_indexes_ptr else [0] * len(codes)
        assert max(texture_indexes) < texture_count
        # Use texture zero, keeping all original glyph pixels untouched.
        texture = ptr(texture_table)
        if not texture:
            shared_names = ptr(table + 16)
            name_ptr = ptr(shared_names)
            name = bytes(system[name_ptr:]).split(b'\0', 1)[0]
            name_at = shared.index(name + b'\0')
            name_ref = shared.index(struct.pack('>I', 0x50000000 + name_at))
            source_texture = name_ref - 24
            texture_blob = bytearray(shared[source_texture:source_texture + 64])
            source_gpu = struct.unpack_from('>I', texture_blob, 28)[0] & 0xfffffff
            gpu_blob = bytearray(shared[source_gpu:source_gpu + 64])
            source_base = (struct.unpack_from('>I', gpu_blob, 32)[0] & ~4095) - 0x60000000
            new_base = (len(physical) + 4095) & ~4095
            physical.extend(bytes(new_base - len(physical)))
            # This shared font atlas is a single 256x256 BC2 level.
            assert struct.unpack_from('>I', gpu_blob, 36)[0] == 0x001fe0ff
            physical.extend(shared[0x3000 + source_base:0x3000 + source_base + 65536])
            old_fetch = struct.unpack_from('>I', gpu_blob, 32)[0]
            struct.pack_into('>I', gpu_blob, 32, 0x60000000 + new_base | (old_fetch & 4095))
            struct.pack_into('>I', texture_blob, 24, alloc(name + b'\0'))
            struct.pack_into('>I', texture_blob, 28, alloc(gpu_blob))
            texture_address = alloc(texture_blob)
            texture = texture_address & 0xfffffff
            struct.pack_into('>I', system, texture_table, texture_address)
            # Local texture pointers have no external shared-name lookup.
            struct.pack_into('>I', system, shared_names, 0)
        fetch = ptr(texture + 28) + 28
        d0, d1, d2 = struct.unpack_from('>3I', system, fetch)
        width, height = (d2 & 8191) + 1, ((d2 >> 13) & 8191) + 1
        assert d0 >> 31 == 1 and d1 & 63 == 19 and (d1 >> 6) & 3 == 1
        pitch = ((d0 >> 22) & 511) * 32 // 4
        base = (d1 & ~4095) - 0x60000000
        # Add a dedicated atlas: all original texture bytes remain unchanged.
        destination_base = (len(physical) + 4095) & ~4095
        physical.extend(bytes(destination_base - len(physical) + 65536))
        new_gpu = bytearray(system[fetch - 28:fetch - 28 + 64])
        struct.pack_into('>I', new_gpu, 32, 0x60000000 + destination_base | (d1 & 4095))
        new_texture = bytearray(system[texture:texture + 64])
        struct.pack_into('>I', new_texture, 24, alloc(f'ptbr-KP Duty JNL-{size}.dds'.encode() + b'\0'))
        struct.pack_into('>I', new_texture, 28, alloc(new_gpu))
        new_texture_ptr = alloc(new_texture)
        old_textures = bytes(system[texture_table:texture_table + 4 * texture_count])
        names_table = ptr(table + 16)
        old_names = bytes(system[names_table:names_table + 4 * texture_count])
        new_textures_ptr = alloc(old_textures + struct.pack('>I', new_texture_ptr))
        new_names_ptr = alloc(old_names + bytes(4))
        struct.pack_into('>I', system, table, new_textures_ptr)
        struct.pack_into('>I', system, table + 16, new_names_ptr)
        struct.pack_into('>HHI', system, table + 20, texture_count + 1, texture_count + 1, texture_count + 1)
        def pixel(x, y, destination=False, source=None):
            offset = (destination_base if destination else base if source is None else source) + tiled_offset(x // 4, y // 4, pitch)
            block = swap16(physical[offset:offset + 16])
            alpha = int.from_bytes(block[:8], 'little')
            return (alpha >> (4 * ((y & 3) * 4 + (x & 3)))) & 15
        def write_pixel(x, y, value):
            offset = destination_base + tiled_offset(x // 4, y // 4, pitch)
            block = swap16(physical[offset:offset + 16])
            alpha = int.from_bytes(block[:8], 'little')
            shift = 4 * ((y & 3) * 4 + (x & 3))
            alpha = (alpha & ~(15 << shift)) | (value << shift)
            block[:8] = alpha.to_bytes(8, 'little')
            block[8:] = bytes.fromhex('ffffffff00000000')
            physical[offset:offset + 16] = swap16(block)
        occupied = []
        for code, plain, accented, base_letter in [(0xd5, 'O', 'Ã', 'A'), (0xf5, 'o', 'ã', 'a')]:
            oi, ai, bi = [codes.index(ord(c)) for c in (plain, accented, base_letter)]
            assert (texture_indexes[oi], texture_indexes[ai], texture_indexes[bi]) == (0, 1, 0)
            accent_gpu = ptr(ptr(texture_table + 4) + 28)
            a0, a1, a2 = struct.unpack_from('>3I', system, accent_gpu + 28)
            assert (a0, a1 & 4095, a2) == (d0, d1 & 4095, d2)
            accent_base = (a1 & ~4095) - 0x60000000
            og, ag, bg = glyphs[oi], glyphs[ai], glyphs[bi]
            pad = int(ag[3] - bg[3])
            assert 1 <= pad <= 12
            w, h = int(og[2]), int(og[3]) + pad
            reserve_w, reserve_h = ((w + 7) // 4) * 4, ((h + 7) // 4) * 4
            chosen = None
            for y in range(height - reserve_h, -1, -4):
                y &= ~3
                for x in range(0, width - reserve_w + 1, 4):
                    if any(x < gx + gw and x + reserve_w > gx and y < gy + gh and y + reserve_h > gy for gx, gy, gw, gh in occupied):
                        continue
                    chosen = (x, y)
                    break
                if chosen:
                    break
            assert chosen, ('KP Duty JNL', size, code, 'no empty atlas region')
            x, y = chosen
            occupied.append((x, y, reserve_w, reserve_h))
            # Copy alpha coverage verbatim. Center the original tilde above O.
            accent_width = int(ag[2])
            accent_shift = (w - accent_width) // 2
            for yy in range(h):
                for xx in range(w):
                    if yy < pad:
                        ax = xx - accent_shift
                        value = pixel(int(ag[0]) + ax, int(ag[1]) + yy, source=accent_base) if 0 <= ax < accent_width else 0
                    else:
                        value = pixel(int(og[0]) + xx, int(og[1]) + yy - pad)
                    write_pixel(x + xx + 1, y + yy + 1, value)
            new = og.copy()
            new[:4] = [float(x + 1), float(y + 1), float(w), float(h)]
            new[5] += ag[5] - bg[5]
            glyphs.append(new)
            texture_indexes.append(texture_count)
        out_glyphs = []
        out_indexes = []
        for c, old_index in zip(expanded, index_map):
            i = old_index if old_index is not None else len(codes) + additions.index(c)
            out_glyphs.append(struct.pack('>8f', *glyphs[i]))
            out_indexes.append(texture_indexes[i])
        struct.pack_into('>I', system, table + 4, alloc(b''.join(out_glyphs)))
        struct.pack_into('>H', system, table + 10, len(expanded))
        struct.pack_into('>I', system, table + 12, alloc(bytes(out_indexes)))
    # RSC5's virtual and physical pointers are relative to separate segments.
    # Grow the virtual segment without changing any original pointer or GPU data offset.
    unit = 1 << (((flags >> 11) & 15) + 8)
    new_size = (len(system) + unit - 1) // unit * unit
    system.extend(bytes(new_size - len(system)))
    flags = (flags & ~0x7ff) | (new_size // unit)
    physical_unit = 1 << (((flags >> 26) & 15) + 8)
    physical_size = (len(physical) + physical_unit - 1) // physical_unit * physical_unit
    physical.extend(bytes(physical_size - len(physical)))
    flags = (flags & ~(0x7ff << 15)) | ((physical_size // physical_unit) << 15)
    return bytes(system + physical), flags


def pack_lzx(data, flags):
    frames = []
    for pos in range(0, len(data), 32768):
        part = data[pos:pos + 32768]
        bits = ('0' if pos == 0 else '') + '011' + f'{len(part) >> 8:016b}' + f'{len(part) & 255:08b}'
        bits += '0' * (-len(bits) % 16)
        header = b''.join(int(bits[i:i+16], 2).to_bytes(2, 'little') for i in range(0, len(bits), 16))
        block = header + struct.pack('<3I', 1, 1, 1) + part
        if len(part) & 1:
            block += b'\0'
        prefix = (b'\xff' + struct.pack('>HH', len(part), len(block))) if len(part) < 32768 else struct.pack('>H', len(block))
        frames.append(prefix + block)
    payload = b''.join(frames)
    return bytes.fromhex('05435352') + struct.pack('>II', 27, flags) + bytes.fromhex('0ff512ef') + struct.pack('>I', len(payload)) + payload


def unpack(raw, decompress):
    assert raw[:4] == b'\x05CSR'
    assert raw[12:16] == bytes.fromhex('0ff512ef')
    pos = 20
    stream = bytearray()
    total = 0
    while pos < len(raw):
        if raw[pos] == 0xff:
            size, length = struct.unpack_from('>HH', raw, pos + 1)
            pos += 5
        else:
            size = 0x8000
            length = struct.unpack_from('>H', raw, pos)[0]
            pos += 2
        if not length:
            break
        assert pos + length <= len(raw), (pos, length, len(raw))
        stream.extend(raw[pos:pos + length])
        total += size
        pos += length
    if total > 4 << 20:
        raise ValueError("Unexpected font resource size")
    output = ctypes.create_string_buffer(total)
    result = decompress(bytes(stream), len(stream), output, total, 0x20000, None, 0)
    if result:
        raise ValueError("Invalid compressed font resource")
    return output.raw

def update(game, runtime):
    api = bp.helpers()
    image = api['decrypted_image'](game / 'default.xex', bp.ROOT / 'third_party/rexglue-sdk')
    key = image[0x7d8ac4:0x7d8ae4]
    archive = game / 'xarchive_cache.rpf'
    header, toc, entries = bp.read_index(archive, api['_aes'], key)
    def read_resource(stream, entry):
        stream.seek(entry[2] & ~255)
        return stream.read(entry[1])
    target = 1927
    entry = entries[target]
    with os.add_dll_directory(str(runtime.resolve())), archive.open('rb') as source:
        library = ctypes.CDLL(str((runtime / 'rexruntime.dll').resolve()))
        decompress = getattr(library, '?lzx_decompress@@YAHPEBX_KPEAX1I21@Z')
        decompress.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t,
                               ctypes.c_uint32, ctypes.c_void_p, ctypes.c_size_t]
        before = unpack(read_resource(source, entry), decompress)
        expected = '99baaa140b7e7b1d6af9193e00daed1ea8105f336e95ffaffa1769c38d5a4b46'
        if hashlib.sha256(before).hexdigest() == expected:
            return
        shared = unpack(read_resource(source, entries[55]), decompress)
        if (hashlib.sha256(before).hexdigest() != 'd6716c4f189aa2227335ffba8fab6a805cab3c5d4653b2aad9445f0f8d109e77'
                or hashlib.sha256(shared).hexdigest() != '01168cff937bdda05ac6121b7ce282bb82768f8b9b4f8f2a27eab62ddfe04145'):
            raise ValueError('Unknown pause font or shared texture archive')
    after, flags = patch(before, shared, entry[3])
    if hashlib.sha256(after).hexdigest() != expected:
        raise ValueError('Generated font differs from the verified atlas')
    packed = pack_lzx(after, flags)
    position = (archive.stat().st_size + 2047) & ~2047
    if position >= 0x80000000:
        raise ValueError('Font resource exceeds RPF3 offset limit')
    temporary = archive.with_suffix('.' + uuid.uuid4().hex + '.tmp')
    try:
        shutil.copyfile(archive, temporary)
        with temporary.open('r+b') as out:
            out.seek(position)
            out.write(packed)
            struct.pack_into('<4I', toc, target * 16, entry[0], len(packed), position | 27, flags)
            disk_toc = bytes(toc)
            if header[4]:
                for _ in range(16):
                    disk_toc = api['_encrypt'](key, disk_toc, 'ChainingModeECB')
            out.seek(0x800)
            out.write(disk_toc)
        _, _, verified = bp.read_index(temporary, api['_aes'], key)
        with temporary.open('rb') as stream:
            if (unpack(read_resource(stream, verified[target]), decompress) != after or
                    any(old != new for i, (old, new) in enumerate(zip(entries, verified)) if i != target)):
                raise ValueError('Font archive verification failed')
        temporary.replace(archive)
    finally:
        temporary.unlink(missing_ok=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', required=True, type=Path)
    parser.add_argument('--runtime', required=True, type=Path)
    options = parser.parse_args()
    update(options.game, options.runtime)
