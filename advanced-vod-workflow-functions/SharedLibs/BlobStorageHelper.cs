//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;


namespace advanced_vod_functions.SharedLibs
{
    public class BlobStorageHelper
    {
        private const string ResourceId = "https://storage.azure.com/"; // Storage resource endpoint
        private const string AuthEndpoint = "https://login.microsoftonline.com/{0}/oauth2/token"; // Azure AD OAuth endpoint

        static public string AmsStorageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
        static public string AmsStorageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

        static public string GetUserOAuthToken(string aadTenantDomain, string aadClientId, string aadClientSecret)
        {
            // Construct the authority string from the Azure AD OAuth endpoint and the tenant ID. 
            string authority = string.Format(CultureInfo.InvariantCulture, AuthEndpoint, aadTenantDomain);
            AuthenticationContext authContext = new AuthenticationContext(authority);
            ClientCredential clientCredential = new ClientCredential(aadClientId, aadClientSecret);

            // Acquire an access token from Azure AD
            AuthenticationResult result = authContext.AcquireTokenAsync(ResourceId, clientCredential).Result;

            return result.AccessToken;
        }

        static public CloudBlobContainer GetCloudBlobContainer(string storageAccountName, string storageAccountKey, string containerName)
        {
            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(storageAccountName, storageAccountKey), true);
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            return cloudBlobClient.GetContainerReference(containerName);
        }

        static public CloudBlobContainer GetCloudBlobContainer(StorageCredentials storageCredentials, string storageAccountName, string containerName)
        {
            CloudStorageAccount storageAccount = new CloudStorageAccount(storageCredentials, storageAccountName, null, true);
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            return cloudBlobClient.GetContainerReference(containerName);
        }


        static public void CopyBlobsAsync(CloudBlobContainer sourceBlobContainer, CloudBlobContainer destinationBlobContainer, List<string> fileNames, TraceWriter log)
        {
            string blobPrefix = null;
            bool useFlatBlobListing = true;
            if (fileNames != null)
            {
                log.Info("Copying listed blob files...");
                foreach (var fileName in fileNames)
                {
                    CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
                    log.Info("Source blob : " + (sourceBlob as CloudBlob).Uri.ToString());
                    CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);
                    if (destinationBlob.Exists())
                    {
                        log.Info("Destination blob already exists. Skipping: " + destinationBlob.Uri.ToString());
                    }
                    else
                    {
                        log.Info("Copying blob " + sourceBlob.Uri.ToString() + " to " + destinationBlob.Uri.ToString());
                        CopyBlobAsync(sourceBlob as CloudBlob, destinationBlob);
                    }
                }
            }
            else
            {
                log.Info("Copying all blobs in the source container...");
                var blobList = sourceBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.None);
                foreach (var sourceBlob in blobList)
                {
                    log.Info("Source blob : " + (sourceBlob as CloudBlob).Uri.ToString());
                    CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference((sourceBlob as CloudBlob).Name);
                    if (destinationBlob.Exists())
                    {
                        log.Info("Destination blob already exists. Skipping: " + destinationBlob.Uri.ToString());
                    }
                    else
                    {
                        log.Info("Copying blob " + sourceBlob.Uri.ToString() + " to " + destinationBlob.Uri.ToString());
                        CopyBlobAsync(sourceBlob as CloudBlob, destinationBlob);
                    }
                }
            }
        }

        static public async void CopyBlobAsync(CloudBlob sourceBlob, CloudBlob destinationBlob)
        {
            var signature = sourceBlob.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24)
            });
            await destinationBlob.StartCopyAsync(new Uri(sourceBlob.Uri.AbsoluteUri + signature));
        }

        static public CopyStatus MonitorBlobContainer(CloudBlobContainer destinationBlobContainer)
        {
            string blobPrefix = null;
            bool useFlatBlobListing = true;
            var destBlobList = destinationBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.Copy);
            CopyStatus copyStatus = CopyStatus.Success;
            foreach (var dest in destBlobList)
            {
                var destBlob = dest as CloudBlob;
                if (destBlob.CopyState.Status == CopyStatus.Aborted || destBlob.CopyState.Status == CopyStatus.Failed)
                {
                    // Log the copy status description for diagnostics and restart copy
                    destBlob.StartCopyAsync(destBlob.CopyState.Source);
                    copyStatus = CopyStatus.Pending;
                }
                else if (destBlob.CopyState.Status == CopyStatus.Pending)
                {
                    // We need to continue waiting for this pending copy
                    // However, let us log copy state for diagnostics
                    copyStatus = CopyStatus.Pending;
                }
                // else we completed this pending copy
            }
            return copyStatus;
        }
    }
}
