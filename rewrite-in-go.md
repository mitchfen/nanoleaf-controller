# Go + HTMX Rewrite Implementation Plan

This document outlines the step-by-step plan for rewriting the **Nanoleaf Controller** Blazor Server application into a stateless, horizontally scalable **Go + HTMX** application.

---

## 📂 Proposed Directory Structure

We will adopt a standard Go project layout:

```text
nanoleaf-controller/
├── cmd/
│   └── controller/
│       └── main.go           # Entrypoint, config, route registration
├── internal/
│   ├── nanoleaf/
│   │   ├── client.go         # REST API wrapper & caching logic
│   │   ├── geometry.go       # SVG layout math (bounding boxes, shape polygons)
│   │   ├── colors.go         # HSV/Hex/Kelvin converters
│   │   └── models.go         # Go representations of JSON payloads
│   └── web/
│       ├── handlers.go       # HTTP controllers (GET /dashboard, PUT /brightness, etc.)
│       └── templates.go      # html/template compile wrapper
├── web/
│   ├── static/
│   │   └── css/
│   │       └── style.css     # UI styles (mostly ported from current style.css)
│   └── templates/
│       ├── base.html         # Main outer layout HTML
│       ├── dashboard.html    # Inner control board and container
│       ├── svg.html          # Dynamic SVG panel visualization fragment
│       └── status.html       # Status stats card fragment
├── Dockerfile                # Multi-stage Go build
├── go.mod
└── go.sum
```

---

## 🛠️ Step-by-Step Migration Plan

### Step 1: Configuration & Modules Setup
1.  Initialize Go module: `go mod init nanoleaf-controller`.
2.  Set up environment variable parsing to support both standard variables and the C# nested structure formats:
    *   `NANOLEAF_IP_ADDRESS` or `Nanoleaf__IpAddress`
    *   `NANOLEAF_AUTH_TOKEN` or `Nanoleaf__AuthToken`
3.  Choose a router (either Go's standard library `net/http` for zero-dependencies, or a simple router like `github.com/go-chi/chi/v5` for clean sub-routing and middleware support).

### Step 2: Define Go Structs & Models
Translate C# representation classes to Go types with exact JSON tags matching the Nanoleaf API format:
*   `State`: Power, brightness, color mode, hue, saturation, color temperature, current effect name.
*   `PanelLayout`: Number of panels, side length, coordinate arrays ($X, Y, O, ShapeType$).
*   `Effect`: Detail structures containing color palettes.

### Step 3: Core API Client with TTL Cache
To ensure that scaling to multiple pods doesn't overwhelm the Nanoleaf device:
1.  Create a `NanoleafClient` struct containing an `http.Client`.
2.  Implement methods: `GetState()`, `GetLayout()`, `SetPower(bool)`, `SetBrightness(int)`, `SetColor(h, s, v)`, `SetEffect(string)`.
3.  Add an **in-memory Cache** with `sync.RWMutex` to cache the state query results.
    *   Cache duration (TTL): **2 seconds**.
    *   If a request comes in and the cache is less than 2 seconds old, serve the cached state.
    *   If a write action (like toggling power) occurs, invalidate the cached state.

### Step 4: Geometry & Color Conversion Library
Port the C# math functions directly:
1.  **Polygon Generation (`geometry.go`):** Port `GetSvgPoints` and `GetPolygonPoints` to calculate coordinates for triangles, rhythm modules, squares, and hexagons.
2.  **ViewBox Calculation:** Port `CalculateViewBox` to calculate the minimum and maximum X/Y coordinates to fit all panels within the dynamic SVG viewBox.
3.  **Color Space Operations (`colors.go`):** Port `HsvToHex`, `HexToHsv`, and the black-body calculation `ColorTemperatureToHex`.

### Step 5: HTML Templates & Fragments
Write the interface templates in `/web/templates/` using Go's built-in `html/template` package:
1.  `base.html`: Page shell with Google Font links, HTMX scripts, and `/web/static/css/style.css`.
2.  `dashboard.html`: The core grid structure.
3.  `svg.html`: An isolated SVG rendering loop:
    ```html
    {{define "svg"}}
    <svg viewBox="{{.ViewBox}}" width="100%" height="100%">
      {{range .Panels}}
      <polygon points="{{.Points}}" fill="{{.Color}}" class="nanoleaf-panel" data-panel-id="{{.Id}}" />
      {{end}}
    </svg>
    {{end}}
    ```
4.  `status.html`: The system metrics block.

### Step 6: Client-Side Features (Reactivity)
To make the application fully stateless, move dynamic/interactive UI loops to the client side:
1.  **Animation Timer:**
    *   Instead of server-side background tickers pushing SVGs, when an effect is selected, the server returns the page with the palette array embedded as a `data-palette` attribute:
        ```html
        <div id="visualization" data-palette='["#ff0000", "#00ff00", "#0000ff"]'>
        ```
    *   A simple JavaScript timer (or Alpine.js component) polls this data, picks random colors, and assigns them to the SVG `<polygon>` elements using CSS transition fades.
2.  **HTMX Polling:**
    *   Use HTMX to poll for device updates (like checking if the light was turned off manually elsewhere):
        ```html
        <div hx-get="/api/status" hx-trigger="every 5s" hx-swap="innerHTML">
        ```
3.  **Slider Debouncing:**
    *   Use HTMX `delay:200ms` attribute modifiers on the range inputs to prevent dragging the slider from sending 100 requests to the server per second:
        ```html
        <input type="range" name="brightness" hx-put="/api/brightness" hx-trigger="change, input delay:200ms" />
        ```

### Step 7: Multi-Stage Dockerfile
Replace the heavy .NET SDK container image with a lightweight, multi-stage compilation:
1.  **Build stage:** `golang:1.22-alpine` to compile the static binary.
2.  **Runtime stage:** `alpine:latest` (or `scratch`).
3.  Copy the static binary and templates.
4.  **Estimated image size reduction:** From **~210MB** down to **~15MB**.

### Step 8: Kubernetes Scale Up
Modify `kubernetes/manifest.yaml`:
1.  Increase replicas: `replicas: 3`.
2.  Remove any session affinity or sticky configuration annotations.
3.  Deploy.
