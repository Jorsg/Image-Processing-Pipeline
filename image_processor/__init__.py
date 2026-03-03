"""
Image Processing Pipeline - Azure Function with Blob Storage trigger.

This function is triggered when a new image is uploaded to the 'images'
container.  It will:
  1. Resize the image to the configured dimensions.
  2. Generate a thumbnail.
  3. Run OCR via Azure Computer Vision.
  4. Produce a SAS token for the original blob.
  5. Persist all metadata to Azure Table Storage.
"""

import io
import json
import logging
import os
import uuid
from datetime import datetime, timezone

import azure.functions as func

from .image_utils import resize_image, create_thumbnail
from .ocr_utils import extract_text
from .storage_utils import (
    upload_blob,
    generate_sas_token,
    save_metadata,
)

app = func.FunctionApp()

# ---------------------------------------------------------------------------
# Configuration (read once at module load time)
# ---------------------------------------------------------------------------
RESIZED_CONTAINER: str = os.environ.get("RESIZED_CONTAINER", "images-resized")
THUMBNAILS_CONTAINER: str = os.environ.get("THUMBNAILS_CONTAINER", "thumbnails")
RESIZE_WIDTH: int = int(os.environ.get("RESIZE_WIDTH", "800"))
RESIZE_HEIGHT: int = int(os.environ.get("RESIZE_HEIGHT", "600"))
THUMBNAIL_SIZE: int = int(os.environ.get("THUMBNAIL_SIZE", "150"))
SAS_EXPIRY_HOURS: int = int(os.environ.get("SAS_EXPIRY_HOURS", "24"))

logger = logging.getLogger(__name__)


@app.blob_trigger(
    arg_name="myblob",
    path="images/{name}",
    connection="STORAGE_CONNECTION_STRING",
)
def process_image(myblob: func.InputStream) -> None:
    """Blob Storage trigger – processes every new image uploaded to 'images'."""

    blob_name: str = myblob.name.split("/")[-1]
    logger.info("Processing blob: %s  size: %d bytes", blob_name, myblob.length)

    # Read the raw image bytes once so we can pass them around cheaply.
    image_bytes: bytes = myblob.read()

    # ------------------------------------------------------------------
    # 1. Resize
    # ------------------------------------------------------------------
    resized_bytes = resize_image(image_bytes, RESIZE_WIDTH, RESIZE_HEIGHT)
    resized_blob_name = f"resized_{blob_name}"
    upload_blob(RESIZED_CONTAINER, resized_blob_name, resized_bytes)
    logger.info("Resized image uploaded as: %s", resized_blob_name)

    # ------------------------------------------------------------------
    # 2. Thumbnail
    # ------------------------------------------------------------------
    thumbnail_bytes = create_thumbnail(image_bytes, THUMBNAIL_SIZE)
    thumb_blob_name = f"thumb_{blob_name}"
    upload_blob(THUMBNAILS_CONTAINER, thumb_blob_name, thumbnail_bytes)
    logger.info("Thumbnail uploaded as: %s", thumb_blob_name)

    # ------------------------------------------------------------------
    # 3. OCR
    # ------------------------------------------------------------------
    ocr_text = extract_text(image_bytes)
    logger.info("OCR complete – extracted %d characters", len(ocr_text))

    # ------------------------------------------------------------------
    # 4. SAS token for the original blob
    # ------------------------------------------------------------------
    images_container: str = os.environ.get("IMAGES_CONTAINER", "images")
    sas_url = generate_sas_token(images_container, blob_name, SAS_EXPIRY_HOURS)
    logger.info("SAS URL generated")

    # ------------------------------------------------------------------
    # 5. Persist metadata to Table Storage
    # ------------------------------------------------------------------
    metadata = {
        "PartitionKey": "images",
        "RowKey": str(uuid.uuid4()),
        "BlobName": blob_name,
        "ResizedBlobName": resized_blob_name,
        "ThumbnailBlobName": thumb_blob_name,
        "OcrText": ocr_text,
        "SasUrl": sas_url,
        "OriginalSize": myblob.length,
        "ProcessedAt": datetime.now(timezone.utc).isoformat(),
    }
    table_name: str = os.environ.get("METADATA_TABLE", "imagemetadata")
    save_metadata(table_name, metadata)
    logger.info("Metadata saved for blob: %s", blob_name)
