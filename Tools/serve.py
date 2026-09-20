"""Static server for the Web build with the cross-origin isolation headers that SharedArrayBuffer
(and therefore Burst/native multithreading) requires. itch.io sets the same headers when
"SharedArrayBuffer support" is enabled on the project page; GitHub Pages cannot.

    python Tools/serve.py [dir=Builds/Web] [port=8080]
"""
import http.server, os, sys

root = sys.argv[1] if len(sys.argv) > 1 else "Builds/Web"
port = int(sys.argv[2]) if len(sys.argv) > 2 else 8080

class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".wasm": "application/wasm",
        ".js": "application/javascript",
        ".data": "application/octet-stream",
    }

    def __init__(self, *a, **kw):
        super().__init__(*a, directory=root, **kw)

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, fmt, *args):
        sys.stdout.write("%s %s\n" % (self.address_string(), fmt % args)); sys.stdout.flush()

os.chdir(os.path.dirname(os.path.abspath(__file__)) + "/..")
print(f"serving {os.path.abspath(root)} on http://localhost:{port} with COOP/COEP", flush=True)
http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
