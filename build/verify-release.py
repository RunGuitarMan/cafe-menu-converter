"""Run the shipped GUI and PDF->XLSX flow from a relocated, dependency-isolated copy."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

parser = argparse.ArgumentParser()
parser.add_argument("artifact", type=Path)
parser.add_argument("report_directory", type=Path)
parser.add_argument("--block-network", action="store_true")
args = parser.parse_args()
artifact = args.artifact.resolve()
report = args.report_directory.resolve()
assert not report.exists() or not any(report.iterdir()), "Choose an empty report directory for each verification run"
report.mkdir(parents=True, exist_ok=True)
root = Path(__file__).resolve().parent.parent
fixtures = root / "tests/CafeMenuConverter.Core.Tests/Fixtures"
windows = sys.platform == "win32"
rule = "CafeMenuConverter-release-check-" + uuid.uuid4().hex
firewall_added = False
ps = str(Path(os.environ.get("SystemRoot", "C:/Windows")) / "System32/WindowsPowerShell/v1.0/powershell.exe")

with tempfile.TemporaryDirectory(prefix="CafeMenu проверка ") as temporary:
    relocated = Path(temporary).resolve()
    if windows:
        exe = relocated / "CafeMenuConverter.exe"
        shutil.copy2(artifact, exe)
        assert list(relocated.iterdir()) == [exe], "Test must start with just the EXE"
    else:
        subprocess.run(["/usr/bin/ditto", "-x", "-k", str(artifact), str(relocated)], check=True)
        app = relocated / "CafeMenuConverter.app"
        subprocess.run(["/usr/bin/codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        exe = app / "Contents/MacOS/CafeMenuConverter.App"
    env = os.environ.copy()
    for key in ("DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DYLD_LIBRARY_PATH", "DYLD_FALLBACK_LIBRARY_PATH"):
        env.pop(key, None)
    for key in ("DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM64", "DOTNET_ROOT(x86)"):
        env[key] = str(relocated / "no-installed-dotnet")
    env["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    env["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = str(relocated / "native-cache")
    env["PATH"] = os.pathsep.join([str(Path(os.environ.get("SystemRoot", "C:/Windows")) / "System32"), os.environ.get("SystemRoot", "C:/Windows")]) if windows else "/usr/bin:/bin"
    command = [str(exe), "--verify-release", str(fixtures), str(report)]
    try:
        if args.block_network:
            if windows:
                escaped = str(exe).replace("'", "''")
                subprocess.run([ps, "-NoProfile", "-Command", f"$ErrorActionPreference='Stop'; New-NetFirewallRule -DisplayName '{rule}' -Direction Outbound -Program '{escaped}' -Action Block | Out-Null"], check=True)
                firewall_added = True
            else:
                command = ["/usr/bin/sandbox-exec", "-p", "(version 1)(allow default)(deny network*)"] + command
        completed = subprocess.run(command, cwd=relocated, env=env, capture_output=True, timeout=120)
        (report / "process.log").write_bytes(completed.stdout + completed.stderr)
        assert completed.returncode == 0, f"Application failed ({completed.returncode}): {(report / 'process.log').read_text(errors='replace')}"
        result = json.loads((report / "report.json").read_text(encoding="utf-8"))
        assert result["success"], result.get("error")
        modules = [p.lower().replace("\\", "/") for p in result["modules"]]
        allowed = [str(relocated).lower().replace("\\", "/") + "/"]
        if windows:
            allowed.append(os.environ.get("SystemRoot", "C:/Windows").lower().replace("\\", "/") + "/")
        else:
            allowed.extend(["/usr/lib/", "/system/library/", "/system/volumes/preboot/cryptexes/os/", "/library/apple/system/library/"])
        for module in modules:
            assert "/shared/microsoft.netcore.app/" not in module and "/.nuget/" not in module, f"External runtime/package loaded: {module}"
            assert any(module.startswith(prefix) for prefix in allowed), f"Dependency outside the app and OS: {module}"
        assert any("skiasharp" in p for p in modules), "Native graphics library did not load"
        # Independently read every output field and compare with the golden PDF references.
        ns = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
        fields = ["date", "mealType", "category", "name", "composition", "weights", "unit", "quantity", "price", "calories", "protein", "fats", "carbohydrates"]
        for menu in ("breakfast", "lunch"):
            expected = json.loads((fixtures / f"expected-{menu}.json").read_text(encoding="utf-8-sig"))
            with ZipFile(report / f"{menu}.xlsx") as xlsx:
                strings = ["".join(el.itertext()) for el in ET.fromstring(xlsx.read("xl/sharedStrings.xml"))]
                rows = ET.fromstring(xlsx.read("xl/worksheets/sheet1.xml")).findall("s:sheetData/s:row", ns)[1:]
                assert len(rows) == len(expected)
                for row, wanted in zip(rows, expected):
                    values = [strings[int(cell.find("s:v", ns).text)] for cell in row]
                    assert len(values) == 14
                    assert values[0] == "a5d27432-a5af-41c6-bc91-9b7ce3ce09b2"
                    for column, key in enumerate(fields, 1):
                        value = wanted.get(key, wanted.get(key[0].upper() + key[1:]))
                        actual = values[column]
                        if key in ("composition", "weights"):
                            assert (json.loads(actual) if actual else []) == value, (menu, column, actual, value)
                        elif key == "date":
                            assert actual == value[:10], (menu, actual, value)
                        else:
                            assert actual == value, (menu, key, actual, value)
        result["artifactSha256"] = hashlib.sha256(artifact.read_bytes()).hexdigest()
        result["networkBlocked"] = args.block_network
        result["all300RowsMatchGoldenReferences"] = True
        (report / "report.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"PASS: {artifact.name}; native GUI; 300 rows; embedded runtime; network blocked={args.block_network}")
    finally:
        if firewall_added:
            subprocess.run([ps, "-NoProfile", "-Command", f"Remove-NetFirewallRule -DisplayName '{rule}'"], check=True)
