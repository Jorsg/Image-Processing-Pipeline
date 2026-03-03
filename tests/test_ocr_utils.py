"""
Tests for ocr_utils.extract_text.
"""

import io
import os
from unittest.mock import MagicMock, patch

import pytest

import image_processor.ocr_utils as ocr_utils
from azure.cognitiveservices.vision.computervision.models import OperationStatusCodes


class TestExtractText:
    def test_returns_empty_when_env_not_set(self):
        os.environ.pop("COMPUTER_VISION_ENDPOINT", None)
        os.environ.pop("COMPUTER_VISION_KEY", None)
        result = ocr_utils.extract_text(b"fake-image")
        assert result == ""

    @patch("image_processor.ocr_utils._get_client")
    def test_returns_recognised_text(self, mock_get_client):
        os.environ["COMPUTER_VISION_ENDPOINT"] = "https://fake.cognitiveservices.azure.com/"
        os.environ["COMPUTER_VISION_KEY"] = "fakekey"

        mock_client = MagicMock()
        mock_get_client.return_value = mock_client

        # Build a fake read response with Operation-Location header.
        mock_read_response = MagicMock()
        mock_read_response.headers = {
            "Operation-Location": "https://fake/operations/op-id-123"
        }
        mock_client.read_in_stream.return_value = mock_read_response

        # Build a fake result with two lines.
        mock_line1 = MagicMock()
        mock_line1.text = "Hello World"
        mock_line2 = MagicMock()
        mock_line2.text = "Python Azure"

        mock_page = MagicMock()
        mock_page.lines = [mock_line1, mock_line2]

        mock_result = MagicMock()
        mock_result.status = OperationStatusCodes.succeeded
        mock_result.analyze_result.read_results = [mock_page]
        mock_client.get_read_result.return_value = mock_result

        result = ocr_utils.extract_text(b"fake-image-bytes")

        assert "Hello World" in result
        assert "Python Azure" in result

    @patch("image_processor.ocr_utils._get_client")
    def test_returns_empty_on_exception(self, mock_get_client):
        os.environ["COMPUTER_VISION_ENDPOINT"] = "https://fake.cognitiveservices.azure.com/"
        os.environ["COMPUTER_VISION_KEY"] = "fakekey"

        mock_client = MagicMock()
        mock_get_client.return_value = mock_client
        mock_client.read_in_stream.side_effect = Exception("Connection error")

        result = ocr_utils.extract_text(b"fake-image-bytes")
        assert result == ""

    @patch("image_processor.ocr_utils._get_client")
    def test_returns_empty_when_status_failed(self, mock_get_client):
        os.environ["COMPUTER_VISION_ENDPOINT"] = "https://fake.cognitiveservices.azure.com/"
        os.environ["COMPUTER_VISION_KEY"] = "fakekey"

        mock_client = MagicMock()
        mock_get_client.return_value = mock_client

        mock_read_response = MagicMock()
        mock_read_response.headers = {
            "Operation-Location": "https://fake/operations/op-id-456"
        }
        mock_client.read_in_stream.return_value = mock_read_response

        mock_result = MagicMock()
        mock_result.status = OperationStatusCodes.failed
        mock_client.get_read_result.return_value = mock_result

        result = ocr_utils.extract_text(b"fake-image-bytes")
        assert result == ""
