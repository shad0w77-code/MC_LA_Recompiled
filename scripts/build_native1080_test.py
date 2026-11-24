"""Relink a local 1080p test against an existing, compatible build's objects."""
import argparse
import ctypes
import json
from pathlib import Path
import subprocess


def windows_args(command):
    count = ctypes.c_int()
    split = ctypes.windll.shell32.CommandLineToArgvW
    split.restype = ctypes.POINTER(ctypes.c_wchar_p)
    pointer = split(command, ctypes.byref(count))
    try:
        return [pointer[i] for i in range(count.value)]
    finally:
        ctypes.windll.kernel32.LocalFree(ctypes.cast(pointer, ctypes.c_void_p))


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, default=root / 'out/build/font-trace')
    parser.add_argument('--output', type=Path, default=root / 'out/native1080-fixed')
    parser.add_argument('--link-only', action='store_true', help='Reuse the test objects after a failed link')
    parser.add_argument('--sources', nargs='+', choices=('mcla_app', 'aspect_ratio', 'settings_menu', 'custom_radio'),
                        help='Recompile only these sources, reusing the other test objects')
    args = parser.parse_args()
    build, output = args.build.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    database = json.loads(subprocess.check_output(['ninja', '-t', 'compdb', '-x'], cwd=build))
    replacements = {}
    for name in ('mcla_app', 'aspect_ratio', 'settings_menu', 'custom_radio'):
        entry = next(e for e in database if e['output'].replace('\\', '/') ==
                     f'CMakeFiles/mcla.dir/src/{name}.cpp.obj')
        command = windows_args(entry['command'])
        destination = str(output / f'{name}.obj')
        command[command.index('-o') + 1] = destination
        if not args.link_only and (args.sources is None or name in args.sources):
            subprocess.run(command, cwd=build, check=True)
        elif not Path(destination).is_file():
            parser.error(f'Missing object: {destination}')
        replacements[entry['output'].replace('\\', '/')] = destination
    link = next(e['command'] for e in database if e['output'] == 'mcla.exe')
    link = link.split('cd . && ', 1)[1].split(' && ', 1)[0]
    command = windows_args(link)
    command[command.index('-o') + 1] = str(output / 'mcla.exe')
    command = [replacements.get(a.replace('\\', '/'), a) for a in command]
    subprocess.run(command, cwd=build, check=True)
    print(output / 'mcla.exe')


if __name__ == '__main__':
    main()
