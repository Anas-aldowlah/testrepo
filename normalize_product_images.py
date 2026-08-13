"""Normalize product images onto transparent 1080x1080 WebP canvases.

Install Pillow once before running:
    python -m pip install Pillow
"""

from __future__ import annotations

from pathlib import Path

try:
    from PIL import Image, ImageOps, UnidentifiedImageError
except ModuleNotFoundError as exc:
    raise SystemExit(
        "Pillow is not installed. Run: python -m pip install Pillow"
    ) from exc


# ---------------------------------------------------------------------------
# Settings: edit these two paths as needed.
# ---------------------------------------------------------------------------
INPUT_DIRECTORY = Path(
    r"C:\Users\WinDows\source\repos\YAGOT\wwwroot\images\products"
)
OUTPUT_DIRECTORY = Path(
    r"C:\Users\WinDows\source\repos\YAGOT\wwwroot\images\products_normalized"
)

CANVAS_SIZE = (1080, 1080)
MAX_PRODUCT_SIZE = (864, 864)
WEBP_QUALITY = 85
WEBP_METHOD = 6  # Slowest encoder mode, producing better compression/quality.


class InvalidImageError(Exception):
    """Raised when Pillow cannot identify or decode an input image."""


def output_path_for(
    output_directory: Path,
    source: Path,
    duplicate_stems: set[str],
) -> Path:
    """Build a deterministic name and avoid jpg/png files sharing one stem."""
    if source.stem.casefold() in duplicate_stems:
        suffix = source.suffix.lstrip(".").lower() or "image"
        return output_directory / f"{source.stem}_{suffix}.webp"
    return output_directory / f"{source.stem}.webp"


def normalize_image(source: Path, destination: Path) -> None:
    """Read, orient, resize, center, and save one image as transparent WebP."""
    try:
        # verify() detects truncated or corrupted files without decoding them fully.
        with Image.open(source) as verification_image:
            verification_image.verify()

        # Reopen after verify(), because verify() intentionally invalidates the image.
        with Image.open(source) as opened_image:
            opened_image.seek(0)  # Use the first frame when the source is animated.
            image = ImageOps.exif_transpose(opened_image).convert("RGBA")
            icc_profile = opened_image.info.get("icc_profile")
    except (UnidentifiedImageError, OSError) as exc:
        raise InvalidImageError from exc

    source_width, source_height = image.size
    if source_width <= 0 or source_height <= 0:
        raise ValueError("Image has invalid dimensions")

    scale = min(
        MAX_PRODUCT_SIZE[0] / source_width,
        MAX_PRODUCT_SIZE[1] / source_height,
    )
    resized_size = (
        max(1, round(source_width * scale)),
        max(1, round(source_height * scale)),
    )

    resized = image.resize(resized_size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))

    position = (
        (CANVAS_SIZE[0] - resized.width) // 2,
        (CANVAS_SIZE[1] - resized.height) // 2,
    )
    canvas.alpha_composite(resized, position)

    save_options: dict[str, object] = {
        "format": "WEBP",
        "quality": WEBP_QUALITY,
        "method": WEBP_METHOD,
        "exact": True,
    }
    if icc_profile:
        save_options["icc_profile"] = icc_profile

    canvas.save(destination, **save_options)


def process_directory(input_directory: Path, output_directory: Path) -> tuple[int, int, int]:
    """Process every valid image directly inside input_directory."""
    input_directory = input_directory.expanduser().resolve()
    output_directory = output_directory.expanduser().resolve()

    if not input_directory.is_dir():
        raise NotADirectoryError(f"Input directory does not exist: {input_directory}")
    if input_directory == output_directory:
        raise ValueError("Input and output directories must be different")

    output_directory.mkdir(parents=True, exist_ok=True)

    processed = 0
    skipped = 0
    failed = 0

    source_files = sorted(
        (path for path in input_directory.iterdir() if path.is_file()),
        key=lambda path: path.name.lower(),
    )
    stem_counts: dict[str, int] = {}
    for source in source_files:
        normalized_stem = source.stem.casefold()
        stem_counts[normalized_stem] = stem_counts.get(normalized_stem, 0) + 1
    duplicate_stems = {stem for stem, count in stem_counts.items() if count > 1}

    for source in source_files:
        destination = output_path_for(output_directory, source, duplicate_stems)
        try:
            normalize_image(source, destination)
        except InvalidImageError:
            skipped += 1
            print(f"[SKIP] Not a valid image: {source.name}")
        except Exception as exc:  # Continue processing the remaining files.
            failed += 1
            print(f"[ERROR] {source.name}: {exc}")
        else:
            processed += 1
            print(f"[OK] {source.name} -> {destination.name}")

    return processed, skipped, failed


def main() -> None:
    try:
        processed, skipped, failed = process_directory(
            INPUT_DIRECTORY,
            OUTPUT_DIRECTORY,
        )
    except (NotADirectoryError, ValueError, OSError) as exc:
        raise SystemExit(f"Cannot process images: {exc}") from exc

    print("\nProcessing complete")
    print(f"Successfully processed: {processed}")
    print(f"Skipped invalid/non-image files: {skipped}")
    print(f"Failed: {failed}")
    print(f"Output directory: {OUTPUT_DIRECTORY.resolve()}")


if __name__ == "__main__":
    main()
