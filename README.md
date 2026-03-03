# Image Processing Pipeline

An **Azure Functions** application that automatically processes images uploaded
to Azure Blob Storage.  Every new image triggers a pipeline that:

1. **Resizes** the image to configurable dimensions and stores it in a separate container.
2. **Generates a thumbnail** (centre-cropped square) and stores it in its own container.
3. **Extracts text (OCR)** using the Azure Computer Vision Read API.
4. **Creates a SAS token** for secure, time-limited read access to the original image.
5. **Persists metadata** (blob names, OCR text, SAS URL, timestamps) to Azure Table Storage.

A [lifecycle management policy](lifecycle_policy.json) is also provided to automatically
tier and delete blobs after defined retention periods.

---

## Architecture

```
Azure Blob Storage
  └── images/          ← upload images here  (Blob trigger)
        │
        ▼
  Azure Function: process_image
        │
        ├──► images-resized/   (resized JPEG)
        ├──► thumbnails/       (square JPEG thumbnail)
        ├──► Computer Vision API  (OCR)
        ├──► SAS token generation
        └──► Table Storage: imagemetadata
```

---

## Project Structure

```
.
├── image_processor/
│   ├── __init__.py        # Azure Function entry-point (Blob trigger)
│   ├── image_utils.py     # Pillow-based resize & thumbnail helpers
│   ├── ocr_utils.py       # Azure Computer Vision OCR integration
│   └── storage_utils.py   # Blob upload, SAS token, Table Storage helpers
├── tests/
│   ├── test_image_utils.py
│   ├── test_ocr_utils.py
│   └── test_storage_utils.py
├── host.json              # Azure Functions host configuration
├── local.settings.json    # Local development settings (not committed to source control)
├── lifecycle_policy.json  # ARM template for Blob Storage lifecycle management
├── pytest.ini
└── requirements.txt
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| Python | 3.10 – 3.12 |
| Azure Functions Core Tools | v4 |
| Azure Storage account | General-purpose v2 |
| Azure Computer Vision resource | Standard tier |

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/Jorsg/Image-Processing-Pipeline.git
cd Image-Processing-Pipeline
```

### 2. Install dependencies

```bash
pip install -r requirements.txt
```

### 3. Configure local settings

Copy `local.settings.json` and fill in your Azure resource values:

| Setting | Description |
|---|---|
| `STORAGE_CONNECTION_STRING` | Full connection string for your Storage account |
| `COMPUTER_VISION_ENDPOINT` | Endpoint URL for your Computer Vision resource |
| `COMPUTER_VISION_KEY` | API key for your Computer Vision resource |
| `IMAGES_CONTAINER` | Source container name (default: `images`) |
| `RESIZED_CONTAINER` | Container for resized images (default: `images-resized`) |
| `THUMBNAILS_CONTAINER` | Container for thumbnails (default: `thumbnails`) |
| `METADATA_TABLE` | Table Storage table name (default: `imagemetadata`) |
| `RESIZE_WIDTH` | Target width in pixels (default: `800`) |
| `RESIZE_HEIGHT` | Target height in pixels (default: `600`) |
| `THUMBNAIL_SIZE` | Thumbnail edge size in pixels (default: `150`) |
| `SAS_EXPIRY_HOURS` | SAS token validity in hours (default: `24`) |

### 4. Run locally

```bash
func start
```

Then upload an image to the `images` container of your configured Storage account
(e.g. using Azure Storage Explorer or the Azure Portal).  The function will trigger
automatically.

---

## Lifecycle Management Policy

Deploy the ARM template to apply automatic blob lifecycle rules:

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file lifecycle_policy.json \
  --parameters storageAccountName=<your-storage-account>
```

| Container | Cool tier after | Archive after | Delete after |
|---|---|---|---|
| `images/` | 30 days | 90 days | 365 days |
| `images-resized/` | — | — | 90 days |
| `thumbnails/` | — | — | 90 days |

---

## Running Tests

```bash
python -m pytest tests/ -v
```

---

## Security Notes

* `local.settings.json` is listed in `.gitignore` and must **never** be committed.
* SAS tokens are scoped to **read-only** access and expire after a configurable number of hours.
* Storage account keys should be rotated regularly and managed via Azure Key Vault in production.
