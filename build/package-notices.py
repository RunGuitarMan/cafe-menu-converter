"""Package optional human-readable notices; copies are also embedded in the EXE."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent.parent
output = root / "artifacts/releases"
output.mkdir(parents=True, exist_ok=True)
with ZipFile(output / "Third-party-notices.zip", "w", ZIP_DEFLATED) as archive:
    archive.write(root / "THIRD-PARTY-NOTICES.txt", "THIRD-PARTY-NOTICES.txt")
    for path in sorted((root / "build/licenses").glob("*.txt")):
        archive.write(path, "licenses/" + path.name)
