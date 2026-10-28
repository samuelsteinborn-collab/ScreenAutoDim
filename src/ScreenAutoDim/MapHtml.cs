namespace ScreenAutoDim;

/// <summary>The page shown in <see cref="MapPickerForm"/>. Leaflet is loaded from a CDN, pinned by version and integrity hash.</summary>
internal static class MapHtml
{
    public const string Page = """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
                integrity="sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=" crossorigin="">
          <style>html, body, #map { height: 100%; margin: 0; }</style>
        </head>
        <body>
          <div id="map"></div>
          <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"
                  integrity="sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=" crossorigin=""></script>
          <script>
            const map = L.map('map').setView([50, 10], 4);
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
              maxZoom: 19,
              attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            }).addTo(map);

            let marker = null;
            function setMarker(lat, lon) {
              if (marker) marker.setLatLng([lat, lon]);
              else marker = L.marker([lat, lon]).addTo(map);
            }

            map.on('click', e => {
              const { lat, lng } = e.latlng.wrap();
              setMarker(lat, lng);
              window.chrome.webview.postMessage(JSON.stringify({ lat: lat, lon: lng }));
            });

            window.showAt = (lat, lon, zoom) => { map.setView([lat, lon], zoom); setMarker(lat, lon); };
          </script>
        </body>
        </html>
        """;
}
