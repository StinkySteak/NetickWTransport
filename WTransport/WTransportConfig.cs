namespace WTransportFfi
{
    [System.Serializable]
    public struct WTransportConfig
    {
        public bool EnableConnectUsingCertHash;
        public string ConnectServerCertHash;

        public bool EnableSsl;
        public string CertificatePath;
        public string KeyPath;
    }
}
