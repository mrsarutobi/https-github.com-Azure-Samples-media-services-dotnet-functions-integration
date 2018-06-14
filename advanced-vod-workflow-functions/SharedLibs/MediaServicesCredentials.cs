//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;


namespace advanced_vod_functions.SharedLibs
{
    public class MediaServicesCredentials
    {

        public string AmsAadTenantDomain
        {
            get { return Environment.GetEnvironmentVariable("AMSAADTenantDomain"); }
        }

        public string AmsClientId
        {
            get { return Environment.GetEnvironmentVariable("AMSClientId"); }
        }

        public string AmsClientSecret
        {
            get { return Environment.GetEnvironmentVariable("AMSClientSecret"); }
        }

        public Uri AmsRestApiEndpoint
        {
            get { return new Uri(Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint")); }
        }
    }
}
