# Runbook

## Starting the Application

1. Launch `CwAssetManager.App.exe`
2. On first run, navigate to **⚙ Configuration** and enter API credentials
3. Click **▶ Run Ingestion** to perform the initial asset pull

## Database

The SQLite database is stored at:
```
%LOCALAPPDATA%\CwAssetManager\cwassets.db
```

To reset: close the app and delete the file. The app will recreate it on next launch.

## Log Files

Rolling JSON logs are written to:
```
%LOCALAPPDATA%\CwAssetManager\Logs\cwassetmanager-YYYYMMDD.log
```

Logs are retained for 30 days.

## Pausing Ingestion

Click **⏸ Pause** in the sidebar to suspend background polling. Click **▶ Resume** to restart.

## Purging Request Logs

In the **Request Log** view, click **Purge 30d+** to remove logs older than 30 days.

## Updating Credentials

Open **⚙ Configuration**, update the relevant fields, and click **Save Configuration**. New credentials take effect on the next ingestion run.
