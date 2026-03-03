"""
Tests for image_utils (resize_image and create_thumbnail).
"""

import io

import pytest
from PIL import Image

from image_processor.image_utils import resize_image, create_thumbnail


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_jpeg_bytes(width: int = 640, height: int = 480) -> bytes:
    """Create a minimal in-memory JPEG image of the given dimensions."""
    img = Image.new("RGB", (width, height), color=(100, 149, 237))
    buf = io.BytesIO()
    img.save(buf, format="JPEG")
    return buf.getvalue()


def _open_jpeg_bytes(data: bytes) -> Image.Image:
    return Image.open(io.BytesIO(data))


# ---------------------------------------------------------------------------
# resize_image
# ---------------------------------------------------------------------------

class TestResizeImage:
    def test_output_fits_within_bounds(self):
        data = _make_jpeg_bytes(1920, 1080)
        result = resize_image(data, 800, 600)
        img = _open_jpeg_bytes(result)
        assert img.width <= 800
        assert img.height <= 600

    def test_aspect_ratio_preserved(self):
        """A 16:9 source should still be ~16:9 after resizing."""
        data = _make_jpeg_bytes(1920, 1080)
        result = resize_image(data, 800, 600)
        img = _open_jpeg_bytes(result)
        original_ratio = 1920 / 1080
        result_ratio = img.width / img.height
        assert abs(original_ratio - result_ratio) < 0.05

    def test_output_is_jpeg(self):
        data = _make_jpeg_bytes(640, 480)
        result = resize_image(data, 320, 240)
        img = _open_jpeg_bytes(result)
        assert img.format == "JPEG"

    def test_small_image_not_upscaled(self):
        """Images already smaller than the target should not be enlarged."""
        data = _make_jpeg_bytes(100, 75)
        result = resize_image(data, 800, 600)
        img = _open_jpeg_bytes(result)
        assert img.width <= 100
        assert img.height <= 75

    def test_returns_bytes(self):
        data = _make_jpeg_bytes(200, 200)
        result = resize_image(data, 100, 100)
        assert isinstance(result, bytes)
        assert len(result) > 0


# ---------------------------------------------------------------------------
# create_thumbnail
# ---------------------------------------------------------------------------

class TestCreateThumbnail:
    def test_output_is_square(self):
        data = _make_jpeg_bytes(640, 480)
        result = create_thumbnail(data, 150)
        img = _open_jpeg_bytes(result)
        assert img.width == 150
        assert img.height == 150

    def test_output_is_jpeg(self):
        data = _make_jpeg_bytes(400, 300)
        result = create_thumbnail(data, 100)
        img = _open_jpeg_bytes(result)
        assert img.format == "JPEG"

    def test_returns_bytes(self):
        data = _make_jpeg_bytes(400, 300)
        result = create_thumbnail(data, 50)
        assert isinstance(result, bytes)
        assert len(result) > 0

    def test_tall_image(self):
        data = _make_jpeg_bytes(200, 600)
        result = create_thumbnail(data, 100)
        img = _open_jpeg_bytes(result)
        assert img.width == 100
        assert img.height == 100

    def test_wide_image(self):
        data = _make_jpeg_bytes(800, 200)
        result = create_thumbnail(data, 100)
        img = _open_jpeg_bytes(result)
        assert img.width == 100
        assert img.height == 100
