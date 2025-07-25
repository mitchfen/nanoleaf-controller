A simple webapp to allow people on my home network to control the lights from a browser. 

<img src="./readme-screenshot.png" width="400"/>

### Install on k3s/k8s
1. Set the appropriate values in [secrets.yaml](./kubernetes/secrets.yaml) in base64 encoding. Make sure there are no sneaky spaces or carriage returns!
2. `kubectl create namespace nanoleaf-controller`
3. `kubectl apply -f kubernetes/secrets.yaml -n nanoleaf-controller`
4. `kubectl apply -f kubernetes/manifest.yaml -n nanoleaf-controller`

### Or run it in docker:
```
docker run -p 8080:8080 `
--rm -it -e Nanoleaf__IpAddress="YOUR_IP" `
-e Nanoleaf__AUTHTOKEN="YOUR_TOKEN" `
ghcr.io/mitchfen/nanoleaf-controller:latest
```