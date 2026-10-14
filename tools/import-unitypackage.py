#!/usr/bin/env python3
"""Despachetează un .unitypackage direct în proiect, păstrând GUID-urile (fișierele .meta).

Un .unitypackage e o arhivă tar.gz cu câte un folder pe asset, numit după GUID:
  <guid>/pathname    calea asset-ului în proiect
  <guid>/asset       conținutul (lipsește la foldere)
  <guid>/asset.meta  meta-ul cu GUID-ul
Îl folosim în loc de AssetDatabase.ImportPackage, care în batchmode e asincron și nu apucă să termine.

Folosire: tools/import-unitypackage.py <pachet.unitypackage> [rădăcina proiectului]
"""
import sys
import tarfile
from pathlib import Path


def main():
    package = Path(sys.argv[1])
    root = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parent.parent
    entries = {}
    with tarfile.open(package, "r:gz") as tar:
        for member in tar.getmembers():
            if not member.isfile():
                continue
            guid, _, name = member.name.lstrip("./").partition("/")
            entries.setdefault(guid, {})[name] = tar.extractfile(member).read()

    written = 0
    for guid, files in entries.items():
        if "pathname" not in files:
            continue
        rel = files["pathname"].decode("utf-8").splitlines()[0].strip()
        target = (root / rel).resolve()
        if root.resolve() not in target.parents:
            raise SystemExit(f"cale în afara proiectului: {rel}")
        if "asset" in files:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(files["asset"])
            written += 1
        else:
            target.mkdir(parents=True, exist_ok=True)
        if "asset.meta" in files:
            Path(str(target) + ".meta").write_bytes(files["asset.meta"])
    print(f"{written} fișiere scrise din {package.name}")


if __name__ == "__main__":
    main()
