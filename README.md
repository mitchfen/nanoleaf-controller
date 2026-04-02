# Nanoleaf Controller

A Blazor Server dashboard for controlling Nanoleaf panels from a browser on your local network.

<img src="./readme-screenshot.png" width="400" alt="Nanoleaf Controller UI" />

## What It Does

- Turns panels on and off
- Adjusts brightness with a debounced slider
- Selects scenes/effects from the device effect list
- Sets a solid color via hue/saturation
- Shows a live SVG panel layout preview based on the Nanoleaf panel map
- Polls the controller every 5 seconds to keep UI state in sync

## Tech Stack

- .NET Blazor Server
- Nanoleaf OpenAPI over local HTTP
- Container image published to GitHub Container Registry

## Configuration

The app reads these settings from configuration (appsettings or environment variables):

- `Nanoleaf:IpAddress`
- `Nanoleaf:AuthToken`

Environment variable equivalents:

- `Nanoleaf__IpAddress`
- `Nanoleaf__AuthToken`

Example `appsettings.json` section:

```json
"Nanoleaf": {
	"IpAddress": "192.168.1.100",
	"AuthToken": "your-token"
}
```

## Run Locally

Prerequisites:

- .NET 10 SDK
- Network access to your Nanoleaf controller

From `src/`:

```bash
dotnet run
```

By default (launch profile `http`) the app runs on:

- `http://0.0.0.0:5009`

## Run with Docker

Use the published image:

```bash
docker run --rm -it \
	-p 8080:8080 \
	-e Nanoleaf__IpAddress="YOUR_IP" \
	-e Nanoleaf__AuthToken="YOUR_TOKEN" \
	ghcr.io/mitchfen/nanoleaf-controller:latest
```

Then open `http://localhost:8080`.

## Build Container Image Locally

From `src/`:

```bash
dotnet workload restore
dotnet publish nanoleaf-controller.csproj -c Release -o ./app
docker build -t ghcr.io/mitchfen/nanoleaf-controller:latest .
```

## Deploy to Kubernetes

1. Base64-encode your Nanoleaf values and update `kubernetes/secrets.yaml`.
2. Create namespace:

```bash
kubectl create namespace nanoleaf-controller
```

3. Apply secret:

```bash
kubectl apply -f kubernetes/secrets.yaml -n nanoleaf-controller
```

4. Apply deployment/service/ingress:

```bash
kubectl apply -f kubernetes/manifest.yaml -n nanoleaf-controller
```

The provided ingress host is `nanoleaf-controller.home` and is configured for Traefik TLS entrypoint `websecure`.

## Notes

- This app is intended for trusted local network use.
- The Nanoleaf API calls are made over HTTP to port `16021` on the controller.