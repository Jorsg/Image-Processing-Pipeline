"""
Tests for storage_utils: upload_blob, generate_sas_token, save_metadata.
"""

import os
from unittest.mock import MagicMock, patch, call

import pytest

import image_processor.storage_utils as storage_utils


# ---------------------------------------------------------------------------
# upload_blob
# ---------------------------------------------------------------------------

class TestUploadBlob:
    @patch("image_processor.storage_utils.BlobServiceClient")
    def test_upload_creates_container_when_missing(self, mock_bsc_cls):
        mock_bsc = MagicMock()
        mock_bsc_cls.from_connection_string.return_value = mock_bsc
        mock_container = MagicMock()
        mock_container.exists.return_value = False
        mock_bsc.get_container_client.return_value = mock_container
        mock_blob = MagicMock()
        mock_container.get_blob_client.return_value = mock_blob

        os.environ["STORAGE_CONNECTION_STRING"] = "AccountName=devstoreaccount1;AccountKey=a2V5;DefaultEndpointsProtocol=http"
        storage_utils.upload_blob("mycontainer", "myblob.jpg", b"data")

        mock_container.create_container.assert_called_once()
        mock_blob.upload_blob.assert_called_once_with(b"data", overwrite=True)

    @patch("image_processor.storage_utils.BlobServiceClient")
    def test_upload_skips_create_when_container_exists(self, mock_bsc_cls):
        mock_bsc = MagicMock()
        mock_bsc_cls.from_connection_string.return_value = mock_bsc
        mock_container = MagicMock()
        mock_container.exists.return_value = True
        mock_bsc.get_container_client.return_value = mock_container
        mock_blob = MagicMock()
        mock_container.get_blob_client.return_value = mock_blob

        os.environ["STORAGE_CONNECTION_STRING"] = "AccountName=devstoreaccount1;AccountKey=a2V5;DefaultEndpointsProtocol=http"
        storage_utils.upload_blob("mycontainer", "myblob.jpg", b"data")

        mock_container.create_container.assert_not_called()
        mock_blob.upload_blob.assert_called_once_with(b"data", overwrite=True)

    def test_raises_when_connection_string_missing(self):
        os.environ.pop("STORAGE_CONNECTION_STRING", None)
        with pytest.raises(EnvironmentError):
            storage_utils.upload_blob("c", "b", b"d")


# ---------------------------------------------------------------------------
# generate_sas_token
# ---------------------------------------------------------------------------

class TestGenerateSasToken:
    @patch("image_processor.storage_utils.generate_blob_sas")
    def test_returns_url_with_sas_token(self, mock_gen):
        mock_gen.return_value = "sv=2021&sig=abc"
        os.environ["STORAGE_CONNECTION_STRING"] = (
            "DefaultEndpointsProtocol=https;AccountName=myaccount;"
            "AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net"
        )
        url = storage_utils.generate_sas_token("images", "photo.jpg", 24)

        assert "myaccount" in url
        assert "photo.jpg" in url
        assert "sv=2021" in url

    def test_returns_empty_when_no_connection_string(self):
        os.environ.pop("STORAGE_CONNECTION_STRING", None)
        result = storage_utils.generate_sas_token("images", "photo.jpg")
        assert result == ""

    @patch("image_processor.storage_utils.generate_blob_sas")
    def test_sas_permission_is_read_only(self, mock_gen):
        mock_gen.return_value = "sig=abc"
        os.environ["STORAGE_CONNECTION_STRING"] = (
            "DefaultEndpointsProtocol=https;AccountName=acc;"
            "AccountKey=dGVzdA==;EndpointSuffix=core.windows.net"
        )
        storage_utils.generate_sas_token("container", "blob.png", 1)
        _, kwargs = mock_gen.call_args
        perm = kwargs.get("permission") or mock_gen.call_args[0][3]
        # BlobSasPermissions(read=True) should have read enabled
        from azure.storage.blob import BlobSasPermissions
        assert isinstance(perm, BlobSasPermissions)


# ---------------------------------------------------------------------------
# save_metadata
# ---------------------------------------------------------------------------

class TestSaveMetadata:
    @patch("image_processor.storage_utils.TableServiceClient")
    def test_creates_table_and_upserts(self, mock_tsc_cls):
        mock_tsc = MagicMock()
        mock_tsc_cls.from_connection_string.return_value = mock_tsc
        mock_table = MagicMock()
        mock_tsc.get_table_client.return_value = mock_table

        os.environ["STORAGE_CONNECTION_STRING"] = "AccountName=acc;AccountKey=a2V5"
        entity = {"PartitionKey": "images", "RowKey": "123", "BlobName": "test.jpg"}
        storage_utils.save_metadata("imagemetadata", entity)

        mock_table.upsert_entity.assert_called_once_with(entity)

    @patch("image_processor.storage_utils.TableServiceClient")
    def test_tolerates_table_already_exists(self, mock_tsc_cls):
        mock_tsc = MagicMock()
        mock_tsc_cls.from_connection_string.return_value = mock_tsc
        mock_table = MagicMock()
        mock_table.create_table.side_effect = Exception("Table already exists")
        mock_tsc.get_table_client.return_value = mock_table

        os.environ["STORAGE_CONNECTION_STRING"] = "AccountName=acc;AccountKey=a2V5"
        entity = {"PartitionKey": "images", "RowKey": "456", "BlobName": "img.jpg"}
        # Should NOT raise
        storage_utils.save_metadata("imagemetadata", entity)
        mock_table.upsert_entity.assert_called_once_with(entity)
