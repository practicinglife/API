# Troubleshooting

## Circuit breaker is open

**Symptom:** Provider status shows ✖ Open and no data is being ingested.

**Cause:** Multiple consecutive API failures triggered the circuit breaker.

**Fix:**
1. Check the log file for the underlying error (usually auth failure or network timeout)
2. Verify credentials in **Configuration**
3. Wait 30 seconds – the circuit will transition to HalfOpen automatically
4. Run ingestion manually with **▶ Run Ingestion**

## 401 Unauthorized from CW Manage

**Cause:** Expired or incorrect API credentials.

**Fix:** Re-enter `companyId`, `publicKey`, `privateKey`, and `clientId` in Configuration.

## 429 Too Many Requests

**Cause:** Exceeded the provider rate limit.

**Fix:** The application automatically honours `Retry-After`. If persistent, reduce the `RequestsPerMinute` setting in Configuration.

## Database locked / migration errors

**Fix:**
1. Close all application instances
2. Delete `%LOCALAPPDATA%\CwAssetManager\cwassets.db`
3. Relaunch – the database will be recreated

## Assets not matching across providers

**Cause:** Hardware identifiers (BiosGuid, SerialNumber, MAC) differ between providers, or hostnames use FQDN in one provider and short name in another.

**Fix:** The identity resolver normalises hostnames by stripping the domain suffix. For persistent mismatches, manually verify the provider records and ensure at least one common hardware identifier is populated.
