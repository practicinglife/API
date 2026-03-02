# Reporting API Excel Import Format

## Overview
To avoid hitting Reporting API rate limits, you can import your initial computer names, companies, sites, and agents from an Excel file. The application will cache this data locally and only query the API when you explicitly request a refresh.

## Excel File Structure

Create an Excel file (.xlsx or .xls) with the following sheets:

### Companies Sheet (Optional)
| CompanyId | CompanyName | CompanyCode |
|-----------|-------------|-------------|
| comp-123  | Acme Corp   | ACME        |
| comp-456  | Beta Inc    | BETA        |

**Columns:**
- `CompanyId` (Required): Unique identifier for the company
- `CompanyName` (Optional): Display name of the company
- `CompanyCode` (Optional): Short code for the company

### Sites Sheet (Optional)
| SiteId   | SiteCode | SiteName      | CompanyId | CompanyName |
|----------|----------|---------------|-----------|-------------|
| site-789 | NYC      | New York HQ   | comp-123  | Acme Corp   |
| site-012 | LAX      | Los Angeles   | comp-123  | Acme Corp   |

**Columns:**
- `SiteId` (Required): Unique identifier for the site
- `SiteCode` (Optional): Short code for the site (used in API calls)
- `SiteName` (Optional): Display name of the site
- `CompanyId` (Optional): Parent company ID
- `CompanyName` (Optional): Parent company name

### Agents Sheet (Optional)
| ComputerName | MachineId | CompanyName | SiteName | SiteCode | SiteId | OperatingSystem | Status | LastSeen | MacAddress | IpAddress |
|--------------|-----------|-------------|----------|----------|--------|-----------------|--------|----------|------------|-----------|
| WS-001       | machine-1 | Acme Corp   | NYC HQ   | NYC      | site-789 | Windows 11    | Online | 2024-01-15 | AA:BB:CC:DD:EE:FF | 192.168.1.100 |

**Columns:**
- `ComputerName` (Required): Computer/device name
- `MachineId` (Optional): Unique machine identifier from Reporting API
- `CompanyName` (Optional): Company name
- `SiteName` (Optional): Site name
- `SiteCode` (Optional): Site code (important for API lookups)
- `SiteId` (Optional): Site ID
- `OperatingSystem` (Optional): Operating system
- `Status` (Optional): Device status
- `LastSeen` (Optional): Last contact date/time
- `MacAddress` (Optional): Primary MAC address
- `IpAddress` (Optional): IP address

## Usage Instructions

### Initial Import
1. Click **"Import Reporting Excel"** button
2. Select your prepared Excel file
3. The application will:
   - Import companies, sites, and agents into the local cache
   - Display the data in the UI immediately
   - Show import statistics

### Loading Cached Data
- **Load Reporting Companies**: Loads companies from cache (no API call)
- **Load Reporting Sites**: Loads sites from cache (no API call)
- **Load Reporting Agents**: Displays all cached agents (no API call)
- **Load Reporting Agents (site)**: Filters cached agents by site code

### Refreshing from API
1. Click **"Refresh from API"** button
2. Confirm the operation (uses API quota)
3. The application will:
   - Fetch current data from Reporting API
   - Compare with cached data
   - Update only changed records
   - Show statistics of what was updated

## Excel Template Example

You can create a simple template:

```
Sheet: Companies
CompanyId,CompanyName,CompanyCode
comp-001,Example Company,EXAMPLE

Sheet: Sites  
SiteId,SiteCode,SiteName,CompanyId,CompanyName
site-001,HQ,Headquarters,comp-001,Example Company

Sheet: Agents
ComputerName,SiteCode,CompanyName,SiteName,OperatingSystem,MacAddress
DESKTOP-001,HQ,Example Company,Headquarters,Windows 11 Pro,AA:BB:CC:DD:EE:FF
```

## Benefits

- **Rate Limit Protection**: Avoid hitting Reporting API limits
- **Offline Work**: View and filter data without API access
- **Performance**: Instant loading from local cache
- **Selective Updates**: Only refresh specific data when needed
- **Historical Data**: Keep snapshots of device information

## Cache Management

The cache is stored in: `%APPDATA%\ConnectWiseManager\connectwise_manager.sqlite`

Cache tables:
- `ReportingCompanyCache`: Companies with last sync timestamp
- `ReportingSiteCache`: Sites with company relationships
- `ReportingAgentCache`: Agent details with all properties

Each record tracks:
- `LastSyncUtc`: When it was last synced
- `CreatedAtUtc`: When it was first added
- `DataSource`: "Excel" or "API"
