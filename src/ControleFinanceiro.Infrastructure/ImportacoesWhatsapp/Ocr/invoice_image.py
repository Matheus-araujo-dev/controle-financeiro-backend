"""Local image OCR. No network calls; input and output live in a disposable directory."""
import csv
import io
import os
import subprocess
import sys
from pathlib import Path
from PIL import Image, ImageOps

Image.MAX_IMAGE_PIXELS = 40_000_000


def recognize(source, output, engine="tesseract"):
    with Image.open(source) as original:
        if original.width * original.height > 40_000_000:
            raise ValueError("Image too large")
        image = original.convert("L")
    lines = []
    directory = Path(output).parent
    # Each band owns its central interval. Overlap protects rows crossing boundaries,
    # but equal purchases at different coordinates must never be deduplicated.
    for start in range(0, image.height, 1500):
        top = max(0, start - 120)
        band = image.crop((0, top, image.width, min(image.height, start + 1620)))
        band = ImageOps.autocontrast(band).point(lambda p: 0 if p < 210 else 255)
        band = band.resize((band.width * 2, band.height * 2))
        path = directory / "band.png"
        band.save(path)
        result = subprocess.run([engine, str(path), "stdout", "-l", "por", "--psm", "6", "tsv"],
                                capture_output=True, check=True, timeout=35,
                                env={**os.environ, "OMP_THREAD_LIMIT": "1"})
        rows = {}
        for word in csv.DictReader(io.StringIO(result.stdout.decode("utf-8")), delimiter="\t", quoting=csv.QUOTE_NONE):
            if word["level"] != "5" or not word["text"].strip():
                continue
            key = (word["block_num"], word["par_num"], word["line_num"])
            rows.setdefault(key, []).append(word)
        for words in rows.values():
            center = sum(int(w["top"]) + int(w["height"]) / 2 for w in words) / len(words) / 2 + top
            if start <= center < start + 1500:
                lines.append((center, " ".join(w["text"] for w in sorted(words, key=lambda w: int(w["left"])))))
        path.unlink()
    Path(output).write_text("\n".join(text for _, text in sorted(lines)), encoding="utf-8")


if __name__ == "__main__":
    recognize(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "tesseract")
