"""
Azure Storage helpers:
  - upload a blob to a container
  - generate a SAS token (URL) for a blob
  - save metadata to Azure Table Storage
"""

import logging
import os
from datetime import datetime, timedelta, timezone
from typing import Any, Dict

from azure.storage.blob import (
    BlobServiceClient,
    BlobSasPermissions,
    generate_blob_sas,
)
from azure.data.tables import TableServiceClient

logger = logging.getLogger(__name__)


def _get_connection_string() -> str:
    conn_str = os.environ.get("STORAGE_CONNECTION_STRING", "")
    if not conn_str:
        raise EnvironmentError("STORAGE_CONNECTION_STRING is not configured.")
    return conn_str


# ---------------------------------------------------------------------------
# Blob helpers
# ---------------------------------------------------------------------------

def upload_blob(container_name: str, blob_name: str, data: bytes) -> None:
    """Upload *data* to *blob_name* inside *container_name*, creating the
    container if it does not exist."""
    conn_str = _get_connection_string()
    service_client = BlobServiceClient.from_connection_string(conn_str)
    container_client = service_client.get_container_client(container_name)

    if not container_client.exists():
        container_client.create_container()
        logger.info("Created container: %s", container_name)

    blob_client = container_client.get_blob_client(blob_name)
    blob_client.upload_blob(data, overwrite=True)
    logger.debug("Uploaded blob '%s' to container '%s'", blob_name, container_name)


def generate_sas_token(
    container_name: str, blob_name: str, expiry_hours: int = 24
) -> str:
    """
    Generate a read-only SAS URL for *blob_name* in *container_name* that
    expires after *expiry_hours* hours.

    The Storage account name and key are derived from the connection string
    stored in ``STORAGE_CONNECTION_STRING``.  Returns an empty string when
    the connection string is not configured.
    """
    conn_str = os.environ.get("STORAGE_CONNECTION_STRING", "")
    if not conn_str:
        logger.warning("STORAGE_CONNECTION_STRING not set; cannot generate SAS URL.")
        return ""

    # Parse account name and key out of the connection string.
    params: Dict[str, str] = {}
    for part in conn_str.split(";"):
        if "=" in part:
            key, _, value = part.partition("=")
            params[key.strip()] = value.strip()

    account_name = params.get("AccountName", "")
    account_key = params.get("AccountKey", "")
    if not account_name or not account_key:
        logger.warning("Unable to parse AccountName/AccountKey from connection string.")
        return ""

    expiry = datetime.now(timezone.utc) + timedelta(hours=expiry_hours)

    sas_token = generate_blob_sas(
        account_name=account_name,
        container_name=container_name,
        blob_name=blob_name,
        account_key=account_key,
        permission=BlobSasPermissions(read=True),
        expiry=expiry,
    )

    endpoint = params.get(
        "BlobEndpoint",
        f"https://{account_name}.blob.core.windows.net",
    )
    return f"{endpoint}/{container_name}/{blob_name}?{sas_token}"


# ---------------------------------------------------------------------------
# Table Storage helper
# ---------------------------------------------------------------------------

def save_metadata(table_name: str, entity: Dict[str, Any]) -> None:
    """Upsert *entity* into the Azure Table Storage table *table_name*,
    creating the table if it does not exist."""
    conn_str = _get_connection_string()
    service_client = TableServiceClient.from_connection_string(conn_str)
    table_client = service_client.get_table_client(table_name)

    try:
        table_client.create_table()
        logger.info("Created table: %s", table_name)
    except Exception:  # pylint: disable=broad-except
        pass  # Table already exists.

    table_client.upsert_entity(entity)
    logger.debug(
        "Metadata upserted – PartitionKey=%s RowKey=%s",
        entity.get("PartitionKey"),
        entity.get("RowKey"),
    )
