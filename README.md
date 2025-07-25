A super basic webapp to allow people on my home network to control the lights from a browser

<img src="./readme-screenshot.png"/>

## How to run it
1. Run (PowerShell)
```ps1
docker run -p 5000:8080 `
--rm -it -e Nanoleaf__IpAddress="YOUR_IP" `
-e Nanoleaf__AUTHTOKEN="YOUR_TOKEN" `
ghcr.io/mitchfen/nanoleaf-controller:latest
```
2. Navigate to http://localhost:5000