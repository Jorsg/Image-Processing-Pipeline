"""
OCR helper: extracts text from image bytes using the Azure Computer Vision API.
"""

import io
import logging
import os
import time

from azure.cognitiveservices.vision.computervision import ComputerVisionClient
from azure.cognitiveservices.vision.computervision.models import OperationStatusCodes
from msrest.authentication import CognitiveServicesCredentials

logger = logging.getLogger(__name__)

_POLL_INTERVAL_SECONDS = 1
_MAX_POLL_ATTEMPTS = 30


def _get_client() -> ComputerVisionClient:
    endpoint: str = os.environ["COMPUTER_VISION_ENDPOINT"]
    key: str = os.environ["COMPUTER_VISION_KEY"]
    return ComputerVisionClient(endpoint, CognitiveServicesCredentials(key))


def extract_text(image_bytes: bytes) -> str:
    """
    Submit *image_bytes* to the Azure Computer Vision Read API and return
    all recognised text as a single newline-separated string.

    Returns an empty string when no text is found or when the Computer
    Vision environment variables are not configured.
    """
    endpoint = os.environ.get("COMPUTER_VISION_ENDPOINT", "")
    key = os.environ.get("COMPUTER_VISION_KEY", "")
    if not endpoint or not key:
        logger.warning(
            "COMPUTER_VISION_ENDPOINT or COMPUTER_VISION_KEY not set; skipping OCR."
        )
        return ""

    try:
        client = _get_client()
        stream = io.BytesIO(image_bytes)

        read_response = client.read_in_stream(stream, raw=True)

        # The operation URL is returned in the response headers.
        operation_location: str = read_response.headers["Operation-Location"]
        operation_id: str = operation_location.split("/")[-1]

        result = None
        for _ in range(_MAX_POLL_ATTEMPTS):
            result = client.get_read_result(operation_id)
            if result.status not in (
                OperationStatusCodes.running,
                OperationStatusCodes.not_started,
            ):
                break
            time.sleep(_POLL_INTERVAL_SECONDS)

        if result is None or result.status != OperationStatusCodes.succeeded:
            logger.warning("OCR did not succeed; status: %s", result and result.status)
            return ""

        lines = []
        for page in result.analyze_result.read_results:
            for line in page.lines:
                lines.append(line.text)

        return "\n".join(lines)

    except Exception as exc:  # pylint: disable=broad-except
        logger.error("OCR failed: %s", exc)
        return ""
