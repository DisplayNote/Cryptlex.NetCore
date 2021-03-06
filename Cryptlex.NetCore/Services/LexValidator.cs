using System;
using Newtonsoft.Json;
using Cryptlex.NetCore.Models;

namespace Cryptlex.NetCore.Services
{
    public class LexValidator
    {
        private readonly LexDataStore _dataStore;

        public LexValidator(LexDataStore dataStore)
        {
            _dataStore = dataStore;
        }
        public int ValidateActivation(string jwt, string publicKey, string licenseKey, string productId, ActivationPayload activationPayload)
        {
            string payload = LexJwtService.VerifyToken(jwt, publicKey);
            if (String.IsNullOrEmpty(payload))
            {
                return LexStatusCodes.LA_FAIL;
            }
            var tempActivationPayload = JsonConvert.DeserializeObject<ActivationPayload>(payload);
            activationPayload.CopyProperties(tempActivationPayload);
            activationPayload.IsValid = true;
            int status;
            if (licenseKey != activationPayload.Key)
            {
                status = LexStatusCodes.LA_FAIL;
            }
            else if (productId != activationPayload.ProductId)
            {
                status = LexStatusCodes.LA_FAIL;
            }
            else if (activationPayload.Fingerprint != LexEncryptionService.Sha256(LicenseManager.SystemInfo.GetFingerPrint()))
            {
                status = LexStatusCodes.LA_E_MACHINE_FINGERPRINT;
            }
            else if (!ValidateTime(activationPayload.IssuedAt, activationPayload.AllowedClockOffset))
            {
                status = LexStatusCodes.LA_E_TIME;
            }
            else
            {
                status = ValidateActivationStatus(productId, activationPayload);
            }
            if (status == LexStatusCodes.LA_OK || status == LexStatusCodes.LA_EXPIRED || status == LexStatusCodes.LA_SUSPENDED || status == LexStatusCodes.LA_GRACE_PERIOD_OVER)
            {
                var now = GetUtcTimestamp();
                _dataStore.SaveValue(productId, LexConstants.KEY_LAST_RECORDED_TIME, now.ToString());
                _dataStore.SaveValue(productId, LexConstants.KEY_ACTIVATION_JWT, jwt);
            }
            else
            {
                _dataStore.SaveValue(productId, LexConstants.KEY_LAST_RECORDED_TIME, activationPayload.IssuedAt.ToString());
            }
            return status;
        }

        public int ValidateActivationStatus(string productId, ActivationPayload activationPayload)
        {
            var now = GetUtcTimestamp();

            if (activationPayload.LeaseExpiresAt != 0 && (activationPayload.LeaseExpiresAt < now || activationPayload.LeaseExpiresAt < activationPayload.IssuedAt))
            {
                _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                return LexStatusCodes.LA_FAIL;
            }

            bool skipGracePeriodCheck = (activationPayload.ServerSyncInterval == 0 || activationPayload.ServerSyncGracePeriodExpiresAt == 0);
            if (!skipGracePeriodCheck && activationPayload.ServerSyncGracePeriodExpiresAt < now)
            {
                return LexStatusCodes.LA_GRACE_PERIOD_OVER;
            }
            else if (activationPayload.ExpiresAt != 0 && activationPayload.ExpiresAt < now)
            {
                return LexStatusCodes.LA_EXPIRED;
            }

            else if (activationPayload.ExpiresAt != 0 && activationPayload.ExpiresAt < activationPayload.IssuedAt)
            {
                return LexStatusCodes.LA_EXPIRED;
            }

            else if (activationPayload.Suspended)
            {
                return LexStatusCodes.LA_SUSPENDED;
            }
            else
            {
                return LexStatusCodes.LA_OK;
            }
        }

        public bool ValidateProductId(string productId)
        {
            Guid guid;
            if (!Guid.TryParse(productId, out guid))
            {
                return false;
            }
            return true;
        }

        public bool ValidateLicenseKey(string licenseKey)
        {
            if (licenseKey.Length < LexConstants.MIN_PRODUCT_KEY_LENGTH)
            {
                return false;
            }
            if (licenseKey.Length > LexConstants.MAX_PRODUCT_KEY_LENGTH)
            {
                return false;
            }
            return true;
        }

        public bool ValidateSuccessCode(int status)
        {
            if (status == LexStatusCodes.LA_OK || status == LexStatusCodes.LA_EXPIRED || status == LexStatusCodes.LA_GRACE_PERIOD_OVER || status == LexStatusCodes.LA_SUSPENDED || status == LexStatusCodes.LA_TRIAL_EXPIRED)
            {
                return true;
            }
            return false;
        }

        public bool ValidateServerSyncAllowedStatusCodes(long status)
        {
            if (status == LexStatusCodes.LA_OK || status == LexStatusCodes.LA_EXPIRED || status == LexStatusCodes.LA_SUSPENDED)
            {
                return true;
            }
            if (status == LexStatusCodes.LA_E_INET || status == LexStatusCodes.LA_E_RATE_LIMIT || status == LexStatusCodes.LA_E_SERVER)
            {
                return true;
            }
            return false;

        }

        private static bool ValidateTime(long timestamp, long allowedClockOffset)
        {
            var now = GetUtcTimestamp();
            var timeDifference = (long)(timestamp - now);
            return timeDifference <= allowedClockOffset;
        }

        public bool ValidateSystemTime(string productId)
        {
            var now = GetUtcTimestamp();
            var lastRecordedTimeStr = _dataStore.GetValue(productId, LexConstants.KEY_LAST_RECORDED_TIME);
            if (lastRecordedTimeStr == null)
                return false;
            if (!ValidateTime((long) Int32.Parse(lastRecordedTimeStr), LexConstants.ALLOWED_CLOCK_OFFSET)) return false;
            _dataStore.SaveValue(productId, LexConstants.KEY_LAST_RECORDED_TIME, now.ToString());
            return true;
        }

        private static long GetUtcTimestamp()
        {
            return (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }
    }
}