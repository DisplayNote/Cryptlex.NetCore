using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

using System.Net;
using System.Net.Http;
using Cryptlex.NetCore.Models;

namespace Cryptlex.NetCore.Services
{
    public class LexActivationService
    {
        private LexDataStore _dataStore;
        private LexValidator _lexValidator;

        public LexActivationService(LexDataStore dataStore, LexValidator lexValidator)
        {
            _dataStore = dataStore;
            _lexValidator = lexValidator;
        }
        public int ActivateFromServer(string productId, string licenseKey, string publicKey, ActivationPayload activationPayload, List<ActivationMeterAttribute> meterAttributes, bool serverSync = false)
        {
            var metadata = new List<ActivationMetadata>();
            string jsonBody = GetActivationRequest(licenseKey, productId, metadata, meterAttributes);
            var httpService = new LexHttpService();
            HttpResponseMessage httpResponse;
            try
            {
                if (serverSync)
                {
                    httpResponse = httpService.UpdateActivation(activationPayload.Id, jsonBody);
                }
                else
                {
                    httpResponse = httpService.CreateActivation(jsonBody);
                }
            }
            catch (Exception exception)
            {
                System.Console.WriteLine(exception.Message);
                return LexStatusCodes.LA_E_INET;
            }


            //             if (serverSync && LexThread.ActivationValidity.count(activationPayload.id) && LexThread.ActivationValidity[activationPayload.id] == false)
            //             {
            // # ifdef LEX_DEBUG

            //                 LexLogger.LogDebug("Ignore the response as user deactivated the key.");
            // #endif
            //                 return LexStatusCodes.LA_FAIL;
            //             }

            if (!httpResponse.IsSuccessStatusCode)
            {
                return ActivationErrorHandler(productId, httpResponse);
            }
            var json = httpResponse.Content.ReadAsStringAsync().Result;
            var activationResponse = JsonConvert.DeserializeObject<ActivationResponse>(json);
            string jwt = activationResponse.ActivationToken;
            return _lexValidator.ValidateActivation(jwt, publicKey, licenseKey, productId, activationPayload);
        }

        public int ActivationErrorHandler(string productId, HttpResponseMessage httpResponse)
        {
            if (httpResponse.StatusCode == HttpStatusCode.InternalServerError || httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return LexStatusCodes.LA_E_SERVER;
            }
            if (httpResponse.StatusCode == (HttpStatusCode)LexConstants.HttpTooManyRequests)
            {
                return LexStatusCodes.LA_E_RATE_LIMIT;
            }
            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                return LexStatusCodes.LA_E_ACTIVATION_NOT_FOUND;
            }
            if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResponse = JsonConvert.DeserializeObject<HttpErrorResponse>(httpResponse.Content.ReadAsStringAsync().Result);
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.ACTIVATION_LIMIT_REACHED)
                {
                    return LexStatusCodes.LA_E_ACTIVATION_LIMIT;
                }
                // server sync fp validation failed
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.INVALID_ACTIVATION_FINGERPRINT)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_MACHINE_FINGERPRINT;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.VM_ACTIVATION_NOT_ALLOWED)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_VM;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.INVALID_PRODUCT_ID)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_PRODUCT_ID;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.INVALID_LICENSE_KEY)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_LICENSE_KEY;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.AUTHENTICATION_FAILED)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_AUTHENTICATION_FAILED;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.COUNTRY_NOT_ALLOWED)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_COUNTRY;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.IP_ADDRESS_NOT_ALLOWED)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_IP;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.REVOKED_LICENSE)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_REVOKED;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.INVALID_LICENSE_TYPE)
                {
                    _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                    return LexStatusCodes.LA_E_LICENSE_TYPE;
                }
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.METER_ATTRIBUTE_USES_LIMIT_REACHED)
                {
                    return LexStatusCodes.LA_E_METER_ATTRIBUTE_USES_LIMIT_REACHED;
                }
                return LexStatusCodes.LA_E_CLIENT;
            }
            return LexStatusCodes.LA_E_INET;
        }

        public int DeactivateFromServer(string productId, ActivationPayload activationPayload)
        {
            var httpService = new LexHttpService();
            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = httpService.DeleteActivation(activationPayload.Id);
            }
            catch (Exception exception)
            {
                System.Console.WriteLine(exception.Message);
                return LexStatusCodes.LA_E_INET;
            }
            if (!httpResponse.IsSuccessStatusCode)
            {
                return DeactivationErrorHandler(productId, httpResponse);
            }
            activationPayload.IsValid = false;
            _dataStore.Reset(productId);
            return LexStatusCodes.LA_OK;
        }

        public int DeactivationErrorHandler(string productId, HttpResponseMessage httpResponse)
        {
            if (httpResponse.StatusCode == HttpStatusCode.InternalServerError || httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return LexStatusCodes.LA_E_SERVER;
            }
            if (httpResponse.StatusCode == (HttpStatusCode)LexConstants.HttpTooManyRequests)
            {
                return LexStatusCodes.LA_E_RATE_LIMIT;
            }
            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _dataStore.ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
                return LexStatusCodes.LA_E_ACTIVATION_NOT_FOUND;
            }
            if (httpResponse.StatusCode == HttpStatusCode.Conflict)
            {
                var errorResponse = JsonConvert.DeserializeObject<HttpErrorResponse>(httpResponse.Content.ReadAsStringAsync().Result);
                if (errorResponse.Code == LexConstants.ActivationErrorCodes.DEACTIVATION_LIMIT_REACHED)
                {
                    return LexStatusCodes.LA_E_DEACTIVATION_LIMIT;
                }
            }
            return LexStatusCodes.LA_E_CLIENT;
        }
        public string GetActivationRequest(string licenseKey, string productId, List<ActivationMetadata> metadata, List<ActivationMeterAttribute> meterAttributes)
        {
            var activationRequest = new ActivationRequest
            {
                Fingerprint = LexEncryptionService.Sha256(LicenseManager.SystemInfo.GetFingerPrint()),
                ProductId = productId,
                Key = licenseKey,
                Os = LicenseManager.SystemInfo.GetOsName(),
                OsVersion = LicenseManager.SystemInfo.GetOsVersion(),
                UserHash = LexEncryptionService.Sha256(LicenseManager.SystemInfo.GetUser()),
                AppVersion = LicenseManager.AppVersion,
                ClientVersion = LicenseManager.ClientVersion,
                VmName = LicenseManager.SystemInfo.GetVmName(),
                Hostname = LicenseManager.SystemInfo.GetHostname(),
                Email = string.Empty,
                Password = string.Empty,
                Metadata = metadata,
                MeterAttributes = meterAttributes
            };
            
            return JsonConvert.SerializeObject(activationRequest);
        }

        public static string GetMetadata(string key, List<Metadata> metadata)
        {
            string normalizedKey = key.ToUpper();
            foreach (var item in metadata)
            {
                if (normalizedKey == item.Key.ToUpper())
                {
                    return item.Value;
                }
            }
            return null;
        }

        public static LicenseMeterAttribute GetLicenseMeterAttribute(string name, List<LicenseMeterAttribute> meterAttributes)
        {
            string normalizedName = name.ToUpper();
            foreach (var item in meterAttributes)
            {
                if (normalizedName == item.Name.ToUpper())
                {
                    return new LicenseMeterAttribute(name, item.AllowedUses, item.TotalUses);
                }
            }
            return null;
        }

        public static bool MeterAttributeExists(string name, List<LicenseMeterAttribute> meterAttributes)
        {
            string normalizedName = name.ToUpper();
            foreach (var item in meterAttributes)
            {
                if (normalizedName == item.Name.ToUpper())
                {
                    return true;
                }
            }
            return false;
        }

        public static ActivationMeterAttribute GetActivationMeterAttribute(string name, List<ActivationMeterAttribute> meterAttributes)
        {
            string normalizedName = name.ToUpper();
            foreach (var item in meterAttributes)
            {
                if (normalizedName == item.Name.ToUpper())
                {
                    return item;
                }
            }
            return null;
        }
    }
}