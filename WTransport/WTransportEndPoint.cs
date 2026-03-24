using Netick;

namespace WTransportFfi
{
    public class WTransportEndPoint : IEndPoint
    {
        public string IPAddress => _ipAddress;

        public int Port => 0;

        private string _ipAddress;

        public void SetIPAddress(string address)
        {
            _ipAddress = address;
        }

        public override string ToString()
        {
            return _ipAddress;
        }
    }
}
