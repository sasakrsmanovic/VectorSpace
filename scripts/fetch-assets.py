#!/usr/bin/env python3
"""Fetch the open-licensed Inter family for consistent native/canvas typography."""
from pathlib import Path
from urllib.request import urlopen, Request

root = Path(__file__).resolve().parents[1]
output = root / "src/VectorSpace.App/Assets/Fonts"
output.mkdir(parents=True, exist_ok=True)
base = "https://raw.githubusercontent.com/google/fonts/main/ofl/inter/"
for remote, local in [("Inter%5Bopsz,wght%5D.ttf", "Inter.ttf"), ("OFL.txt", "OFL.txt")]:
    destination = output / local
    if destination.exists():
        continue
    with urlopen(Request(base + remote, headers={"User-Agent": "VectorSpace-build"}), timeout=30) as response:
        data = response.read()
    if not data:
        raise RuntimeError("The font response was empty.")
    destination.write_bytes(data)
    print(f"Fetched {local}: {len(data)} bytes")
