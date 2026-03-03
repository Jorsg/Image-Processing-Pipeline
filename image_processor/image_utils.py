"""
Image utility helpers: resizing and thumbnail generation using Pillow.
"""

import io
from PIL import Image


def resize_image(image_bytes: bytes, width: int, height: int) -> bytes:
    """
    Resize *image_bytes* to fit within *width* × *height* while preserving
    the original aspect ratio (thumbnail fit).

    Returns the result as a JPEG-encoded byte string.
    """
    with Image.open(io.BytesIO(image_bytes)) as img:
        img = img.convert("RGB")
        img.thumbnail((width, height), Image.LANCZOS)
        output = io.BytesIO()
        img.save(output, format="JPEG", quality=85, optimize=True)
        return output.getvalue()


def create_thumbnail(image_bytes: bytes, size: int) -> bytes:
    """
    Create a square thumbnail of *size* × *size* pixels by cropping the
    centre of the image first, then resizing.

    Returns the result as a JPEG-encoded byte string.
    """
    with Image.open(io.BytesIO(image_bytes)) as img:
        img = img.convert("RGB")

        # Centre-crop to a square.
        min_dim = min(img.width, img.height)
        left = (img.width - min_dim) // 2
        top = (img.height - min_dim) // 2
        img = img.crop((left, top, left + min_dim, top + min_dim))

        img = img.resize((size, size), Image.LANCZOS)

        output = io.BytesIO()
        img.save(output, format="JPEG", quality=85, optimize=True)
        return output.getvalue()
