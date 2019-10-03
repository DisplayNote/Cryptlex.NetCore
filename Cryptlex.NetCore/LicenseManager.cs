
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cryptlex.NetCore;
using Cryptlex.NetCore.Contracts;
using Cryptlex.NetCore.Models;
using Cryptlex.NetCore.Services;
using Org.BouncyCastle.Crypto.Paddings;

namespace Cryptlex.NetCore
{
    public class LicenseManager
    {
        private string _rsaPublicKey;
        private Timer _timer;
        public delegate void CallbackType(int status);
        private CallbackType _callback;
        private readonly LexActivationService _lexActivationService;
        private ActivationPayload _activationPayload;
        private LexValidator LexValidator { get; set; }
        private LexDataStore DataStore { get; set; }
        
        public static ISystemInfo SystemInfo { get; set; }
        public static string ClientVersion { get; set; }
        public static string AppVersion { get; set; }
        public string ProductId
        {
            get => ProductId;
            set
            {
                if (!LexValidator.ValidateProductId(value))
                {
                    throw new LexActivatorException(LexStatusCodes.LA_E_PRODUCT_ID);
                }
                ProductId = value;
            }
        }

        public string LicenceKey
        {
            get
            {
                if (!string.IsNullOrEmpty(LicenceKey)) return LicenceKey;
                LicenceKey = DataStore.GetValue(ProductId, LexConstants.KEY_LICENSE_KEY);
                if (string.IsNullOrEmpty(LicenceKey))
                {
                    throw new LexActivatorException(LexStatusCodes.LA_E_LICENSE_KEY);
                }

                return LicenceKey;
            }
            set
            {
                if (!LexValidator.ValidateLicenseKey(value))
                {
                    throw new LexActivatorException(LexStatusCodes.LA_E_LICENSE_KEY);
                }
                
                LicenceKey = value;
                DataStore.SaveValue(ProductId, LexConstants.KEY_LICENSE_KEY, LicenceKey);
            }
        }

        public LicenseManager(IPersistence persistence, ISystemInfo systemInfo)
        {
            DataStore = new LexDataStore(persistence);
            LexValidator = new LexValidator(DataStore);
            SystemInfo = systemInfo;
            
            _lexActivationService = new LexActivationService(DataStore, LexValidator);
        }
        /// <summary>
        /// Sets the RSA public key.
        /// 
        /// This function must be called on every start of your program
        /// before any other functions are called.
        /// </summary>
        /// <param name="path">path of the RSA public key file</param>
        public void SetRsaPublicKey(string path)
        {
            if (!File.Exists(path))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_FILE_PATH);
            }
            
            _rsaPublicKey = File.ReadAllText(path, Encoding.UTF8);
        }

        public void SetRsaPublicKeyContents(string contents)
        {
            if (string.IsNullOrEmpty(contents))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_RSA_PUBLIC_KEY);
            }

            _rsaPublicKey = contents;
        }

        /// <summary>
        /// Sets server sync callback function.
        /// 
        /// Whenever the server sync occurs in a separate thread, and server returns the response,
        /// license callback function gets invoked with the following status codes:
        /// LA_OK, LA_EXPIRED, LA_SUSPENDED, LA_E_REVOKED, LA_E_ACTIVATION_NOT_FOUND,
        /// LA_E_MACHINE_FINGERPRINT, LA_E_AUTHENTICATION_FAILED, LA_E_COUNTRY, LA_E_INET,
        /// LA_E_SERVER, LA_E_RATE_LIMIT, LA_E_IP
        /// </summary>
        /// <param name="callback"></param>
        public void SetLicenseCallback(CallbackType callback)
        {
            if (String.IsNullOrEmpty(ProductId))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_PRODUCT_ID);
            }
            _callback = callback;
        }
        
        /// <summary>
        /// Gets the license metadata of the license.
        /// </summary>
        /// <param name="key">metadata key to retrieve the value</param>
        /// <returns>Returns the value of metadata for the key.</returns>
        public string GetLicenseMetadata(string key)
        {
            var status = IsLicenseValid();
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            var value = LexActivationService.GetMetadata(key, _activationPayload.LicenseMetadata);
            if (value == null)
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_METADATA_KEY_NOT_FOUND);
            }
            return value;
        }

        /// <summary>
        /// Gets the license meter attribute allowed uses and total uses.
        /// </summary>
        /// <param name="name">name of the meter attribute</param>
        /// <returns>Returns the values of meter attribute allowed and total uses.</returns>
        public LicenseMeterAttribute GetLicenseMeterAttribute(string name)
        {
            var status = IsLicenseValid();
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            var licenseMeterAttribute = LexActivationService.GetLicenseMeterAttribute(name, _activationPayload.LicenseMeterAttributes);
            if (licenseMeterAttribute == null)
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_METER_ATTRIBUTE_NOT_FOUND);
            }
            return licenseMeterAttribute;
        }

        /// <summary>
        /// Gets the license expiry date timestamp.
        /// </summary>
        /// <returns>Returns the timestamp.</returns>
        public long GetLicenseExpiryDate()
        {
            var status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status))
            {
                return _activationPayload.ExpiresAt;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// Gets the email associated with the license user.
        /// </summary>
        /// <returns>Returns the license user email.</returns>
        public string GetLicenseUserEmail()
        {
            var status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status))
            {
                return _activationPayload.Email;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// Gets the name associated with the license user.
        /// </summary>
        /// <returns>Returns the license user name.</returns>
        public string GetLicenseUserName()
        {
            int status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status))
            {
                return _activationPayload.Name;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// Gets the company associated with the license user.
        /// </summary>
        /// <returns>Returns the license user company.</returns>
        public string GetLicenseUserCompany()
        {
            int status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status))
            {
                return _activationPayload.Company;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// Gets the metadata associated with the license user.
        /// </summary>
        /// <param name="key">key to retrieve the value</param>
        /// <returns>Returns the value of metadata for the key.</returns>
        public string GetLicenseUserMetadata(string key)
        {
            var status = IsLicenseValid();
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            var value = LexActivationService.GetMetadata(key, _activationPayload.UserMetadata);
            if (value == null)
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_METADATA_KEY_NOT_FOUND);
            }
            return value;
        }

        /// <summary>
        /// Gets the license type (node-locked or hosted-floating).
        /// </summary>
        /// <returns>Returns the license type.</returns>
        public string GetLicenseType()
        {
            var status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status))
            {
                return _activationPayload.Type;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// Gets the meter attribute uses consumed by the activation.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>Returns the value of meter attribute uses by the activation.</returns>
        public long GetActivationMeterAttributeUses(string name)
        {
            var status = IsLicenseValid();
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            if (!LexActivationService.MeterAttributeExists(name, _activationPayload.LicenseMeterAttributes))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_METER_ATTRIBUTE_NOT_FOUND);
            }
            var activationMeterAttribute = LexActivationService.GetActivationMeterAttribute(name, _activationPayload.ActivationMeterAttributes);
            return activationMeterAttribute?.Uses ?? 0;
        }

        /// <summary>
        /// Activates the license by contacting the Cryptlex servers. It
        /// validates the key and returns with encrypted and digitally signed token
        /// which it stores and uses to activate your application.
        /// 
        /// This function should be executed at the time of registration, ideally on
        /// a button click.
        /// </summary>
        /// <returns>LA_OK, LA_EXPIRED, LA_SUSPENDED, LA_FAIL</returns>
        public int ActivateLicense()
        {
            if (string.IsNullOrEmpty(ProductId))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_PRODUCT_ID);
            }
            if (string.IsNullOrEmpty(_rsaPublicKey))
            {
                throw new LexActivatorException(LexStatusCodes.LA_E_RSA_PUBLIC_KEY);
            }

            _activationPayload = new ActivationPayload();
            var meterAttributes = new List<ActivationMeterAttribute>();
            var status = _lexActivationService.ActivateFromServer(ProductId, LicenceKey, _rsaPublicKey, _activationPayload, meterAttributes);
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            StartTimer(_activationPayload.ServerSyncInterval, _activationPayload.ServerSyncInterval);
            return status;
        }

        /// <summary>
        /// Deactivates the license activation and frees up the corresponding activation
        /// slot by contacting the Cryptlex servers.
        /// 
        /// This function should be executed at the time of de-registration, ideally on
        /// a button click.
        /// </summary>
        /// <returns>LA_OK</returns>
        public int DeactivateLicense()
        {
            var status = IsLicenseValid();
            if (!LexValidator.ValidateSuccessCode(status)) throw new LexActivatorException(status);
            status = _lexActivationService.DeactivateFromServer(ProductId, _activationPayload);
            if (status == LexStatusCodes.LA_OK)
            {
                return status;
            }
            throw new LexActivatorException(status);
        }

        /// <summary>
        /// It verifies whether your app is genuinely activated or not. The verification is
        /// done locally by verifying the cryptographic digital signature fetched at the time of activation.
        /// 
        /// After verifying locally, it schedules a server check in a separate thread. After the
        /// first server sync it periodically does further syncs at a frequency set for the license.
        /// 
        /// In case server sync fails due to network error, and it continues to fail for fixed
        /// number of days (grace period), the function returns LA_GRACE_PERIOD_OVER instead of LA_OK.
        /// 
        /// This function must be called on every start of your program to verify the activation
        /// of your app.
        /// </summary>
        /// <returns>LA_OK, LA_EXPIRED, LA_SUSPENDED, LA_GRACE_PERIOD_OVER, LA_FAIL</returns>
        public int IsLicenseGenuine()
        {
            var status = IsLicenseValid();
            if (LexValidator.ValidateSuccessCode(status) && _activationPayload.ServerSyncInterval != 0)
            {
                StartTimer(LexConstants.SERVER_SYNC_DELAY, _activationPayload.ServerSyncInterval);
            }
            switch (status)
            {
                case LexStatusCodes.LA_OK:
                    return LexStatusCodes.LA_OK;
                case LexStatusCodes.LA_EXPIRED:
                    return LexStatusCodes.LA_EXPIRED;
                case LexStatusCodes.LA_SUSPENDED:
                    return LexStatusCodes.LA_SUSPENDED;
                case LexStatusCodes.LA_GRACE_PERIOD_OVER:
                    return LexStatusCodes.LA_GRACE_PERIOD_OVER;
                case LexStatusCodes.LA_FAIL:
                    return LexStatusCodes.LA_FAIL;
                default:
                    throw new LexActivatorException(status);
            }
        }

        /// <summary>
        /// It verifies whether your app is genuinely activated or not. The verification is
        /// done locally by verifying the cryptographic digital signature fetched at the time of activation.
        /// 
        /// This is just an auxiliary function which you may use in some specific cases, when you
        /// want to skip the server sync.
        /// 
        /// NOTE: You may want to set grace period to 0 to ignore grace period.
        /// </summary>
        /// <returns>LA_OK, LA_EXPIRED, LA_SUSPENDED, LA_GRACE_PERIOD_OVER, LA_FAIL</returns>
        private int IsLicenseValid()
        {
            if (string.IsNullOrEmpty(ProductId))
            {
                return LexStatusCodes.LA_E_PRODUCT_ID;
            }
            if (!LexValidator.ValidateSystemTime(ProductId))
            {
                return LexStatusCodes.LA_E_TIME_MODIFIED;
            }
            
            var jwt = DataStore.GetValue(ProductId, LexConstants.KEY_ACTIVATION_JWT);
            if (string.IsNullOrEmpty(jwt))
            {
                return LexStatusCodes.LA_FAIL;
            }
            if (_activationPayload != null && _activationPayload.IsValid)
            {
                return LexValidator.ValidateActivationStatus(ProductId, _activationPayload);
            }
            _activationPayload = new ActivationPayload();
            return LexValidator.ValidateActivation(jwt, _rsaPublicKey, LicenceKey, ProductId, _activationPayload);
        }

        /// <summary>
        /// Increments the meter attribute uses of the activation.
        /// </summary>
        /// <param name="name">name of the meter attribute</param>
        /// <param name="increment">the increment value</param>
        public void IncrementActivationMeterAttributeUses(string name, uint increment)
        {
            var currentUses = GetActivationMeterAttributeUses(name);
            var uses = currentUses + increment;
            var meterAttributes = _activationPayload.ActivationMeterAttributes;
            var status = UpdateMeterAttributeUses(name, meterAttributes, uses);
            if (!LexValidator.ValidateSuccessCode(status))
            {
                throw new LexActivatorException(status);
            }
        }

        /// <summary>
        /// Decrements the meter attribute uses of the activation.
        /// </summary>
        /// <param name="name">name of the meter attribute</param>
        /// <param name="decrement">the decrement value</param>
        public void DecrementActivationMeterAttributeUses(string name, uint decrement)
        {
            var currentUses = GetActivationMeterAttributeUses(name);
            if (decrement > currentUses)
            {
                decrement = (uint)currentUses;
            }
            var uses = currentUses - decrement;
            var meterAttributes = _activationPayload.ActivationMeterAttributes;
            var status = UpdateMeterAttributeUses(name, meterAttributes, uses);
            if (!LexValidator.ValidateSuccessCode(status))
            {
                throw new LexActivatorException(status);
            }
        }

        /// <summary>
        /// Resets the meter attribute uses consumed by the activation.
        /// </summary>
        /// <param name="name">name of the meter attribute</param>
        public void ResetActivationMeterAttributeUses(string name)
        {
            var currentUses = GetActivationMeterAttributeUses(name);
            var meterAttributes = _activationPayload.ActivationMeterAttributes;
            var status = UpdateMeterAttributeUses(name, meterAttributes, 0);
            if (!LexValidator.ValidateSuccessCode(status))
            {
                throw new LexActivatorException(status);
            }
        }

        private int UpdateMeterAttributeUses(string name, List<ActivationMeterAttribute> meterAttributes, long uses)
        {
            var normalizedName = name.ToUpper();
            var exists = false;
            foreach (var item in meterAttributes.Where(item => normalizedName == item.Name.ToUpper()))
            {
                item.Uses = uses; ;
                exists = true;
                break;
            }
            if (!exists)
            {
                meterAttributes.Add(new ActivationMeterAttribute(name, uses));
            }

            var status = _lexActivationService.ActivateFromServer(ProductId, LicenceKey, _rsaPublicKey, _activationPayload, meterAttributes, true);
            return status;
        }

        private void LicenseTimerCallback(Object stateInfo)
        {
            if (_activationPayload.IsValid == false)   // invalid as license was dropped
            {
                StopTimer();
                return;
            }
            var meterAttributes = new List<ActivationMeterAttribute>();
            var status = _lexActivationService.ActivateFromServer(ProductId, LicenceKey, _rsaPublicKey, _activationPayload, meterAttributes, true);
            if (!LexValidator.ValidateServerSyncAllowedStatusCodes(status))
            {
                StopTimer();
                _callback(status);
                return;
            }
            _callback(status);
        }

        private void StartTimer(long dueTime, long interval)
        {
            if (_callback == null) return;
            if(_timer != null)
            {
                return;
            }
            _timer = new Timer(LicenseTimerCallback, null, dueTime * 1000, interval * 1000);
        }

        private void StopTimer()
        {
            if (_timer == null) return;
            _timer.Dispose();
            _timer = null;
        }
    }
}