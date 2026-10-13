"""Validate the actual .NET bundle, not just the publish command's arguments."""
from pathlib import Path
import json
import struct
import sys

rid, directory = sys.argv[1:]
folder = Path(directory)
name = "CafeMenuConverter.App" + (".exe" if rid == "win-x64" else "")
data = (folder / name).read_bytes()
marker = bytes.fromhex("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae")
position = data.find(marker)
assert position >= 8, "Missing single-file bundle marker"
position = struct.unpack_from("<q", data, position - 8)[0]

def take(fmt):
    global position
    values = struct.unpack_from(fmt, data, position)
    position += struct.calcsize(fmt)
    return values

def text():
    global position
    length = shift = 0
    while True:
        byte = data[position]
        position += 1
        length |= (byte & 127) << shift
        if byte < 128:
            break
        shift += 7
    result = data[position:position + length].decode("utf-8")
    position += length
    return result

major, minor, count = take("<IIi")
assert major == 6, f"Recheck bundle parser for format {major}.{minor}"
text()
deps_offset, deps_size, config_offset, config_size, flags = take("<qqqqQ")
config = json.loads(data[config_offset:config_offset + config_size])["runtimeOptions"]
assert "framework" not in config and "frameworks" not in config, "External .NET runtime dependency"
assert any(f["name"] == "Microsoft.NETCore.App" and f["version"] == "10.0.12"
           for f in config["includedFrameworks"]), "Expected runtime not included"
entries = {}
for _ in range(count):
    offset, size, compressed = take("<qqq")
    kind, = take("<B")
    entries[text()] = kind
for required in ("System.Private.CoreLib.dll", "CafeMenuConverter.Core.dll", "DocumentFormat.OpenXml.dll", "UglyToad.PdfPig.dll"):
    assert required in entries, f"Missing {required}"
assert not (folder / "categories.json").exists(), "Release must work with embedded defaults"
if rid == "win-x64":
    assert sorted(p.name for p in folder.iterdir()) == [name], "Windows publish must contain exactly one EXE"
    for required in ("av_libglesv2.dll", "libHarfBuzzSharp.dll", "libSkiaSharp.dll"):
        assert entries.get(required) == 2, f"Native dependency not embedded: {required}"
else:
    for required in ("libAvaloniaNative.dylib", "libHarfBuzzSharp.dylib", "libSkiaSharp.dylib"):
        assert (folder / required).is_file(), f"Missing native dependency: {required}"
    assert all(kind != 2 for kind in entries.values()), "macOS libraries must be signed inside the bundle"
print(f"{rid}: self-contained .NET 10.0.12, {count} bundled files; package OK")
