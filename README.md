## Feature
WebTransport for Netick using custom wrapper using [wtransport](https://github.com/BiagioFesta/wtransport)

## Installation

### Prerequisites

Unity Editor version 2021 or later.

Install Netick 2 before installing this package.
https://github.com/NetickNetworking/NetickForUnity

### Dependencies
- `com.unity.nuget.newtonsoft-json`

### Steps

#### Install Transport
- Open the Unity Package Manager by navigating to Window > Package Manager along the top bar.
- Click the plus icon.
- Select Add package from git URL
- Enter https://github.com/StinkySteak/NetickWTransport.git
- You can then create an instance by by double clicking in the Assets folder and going
- Create > Netick > Transport > WTransportProvider


## Development Build

WebTransport require a secure connection in order to establish. However, there are couple of ways to do insecure connection. The easiest way is generate a self-signed certificate when running a webtransport server, and then feed the self-signed certificate hash into the Browser WebTransport (javascript) config