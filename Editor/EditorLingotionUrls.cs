// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Single source of truth for the Lingotion web endpoints used by the editor tooling.
    /// </summary>
    public static class EditorLingotionUrls
    {
        /// <summary>
        /// Root of the Lingotion portal, without a trailing slash. Use as prefix when building endpoints.
        /// </summary>
        public const string PortalRoot = "https://portal.lingotion.com";

        /// <summary>
        /// Portal landing page, suitable for opening in a browser.
        /// </summary>
        public const string PortalHome = PortalRoot + "/";

        /// <summary>
        /// Endpoint used to verify a license key.
        /// </summary>
        public const string LicenseVerify = PortalRoot + "/v1/licenses/verify";

        /// <summary>
        /// Builds the portal account activation URL for this package install.
        /// </summary>
        /// <param name="origin">Where the user got the package from, e.g. "assetstore".</param>
        /// <param name="client">URL-escaped client capability string.</param>
        public static string Activate(string origin, string client)
            => $"{PortalRoot}/activate?platform=unity&origin={origin}&client={client}";

        /// <summary>
        /// Builds the endpoint used to redeem a license download token.
        /// </summary>
        /// <param name="token">The download token to redeem.</param>
        public static string RedeemLicenseToken(string token)
            => $"{PortalRoot}/v1/license-tokens/{token}/redeem";
    }
}
