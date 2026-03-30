## Feature
WebTransport for Netick using custom wrapper using [wtransport](https://github.com/BiagioFesta/wtransport)

## Installation

### Prerequisites

Unity Editor version 2021 or later.

Install Netick 2 before installing this package.
https://github.com/NetickNetworking/NetickForUnity

### Steps

#### Install Transport
- Open the Unity Package Manager by navigating to Window > Package Manager along the top bar.
- Click the plus icon.
- Select Add package from git URL
- Enter https://github.com/StinkySteak/NetickWTransport.git
- You can then create an instance by by double clicking in the Assets folder and going
- Create > Netick > Transport > WTransportProvider

## Features
| Feature             | Remarks     |
|---------------------|-------------|
| WebTransport Server | Supported   |
| WebTransport Client | WebGL Only  |
| Connection Payload  | Unsupported |

## Production Build (TLS)

In Order to allow TLS for production build in your game, you can fill the certificate path here e.g `fullchain.pem` and `privkey.pem` and toggle `EnableSsl`

```
public bool EnableSsl;
public string CertificatePath;
public string KeyPath;
```

## Development Testing

WebTransport requires a secure connection (TLS) to function. For development purposes, the easiest workaround is to generate a self-signed certificate when starting the server. The certificate's SHA-256 hash can then be passed into the browser's WebTransport constructor to bypass the usual certificate validation.
Here's the example code
```cs
string cert = WTransportNative.wtransport_start_server_dev(port);

Debug.Log($"[{nameof(WTransportNetManager)}]: Starting server on: {port}... cert: {cert}");
```

Then pass the cert hash to the JavaScript
```js
const transport = new WebTransport("https://localhost:7777", {
  serverCertificateHashes: [
    {
      algorithm: "sha-256",
      value: certificateHash
    }
  ]
});
```

## License
This project is licensed for use with [Netick](https://netick.net) only.
See [LICENSE](./LICENSE) for details.
