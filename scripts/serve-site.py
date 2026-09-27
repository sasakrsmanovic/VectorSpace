#!/usr/bin/env python3
"""Local static server with the same /VectorSpace/ base path as GitHub Pages."""
import argparse, functools, http.server
parser = argparse.ArgumentParser()
parser.add_argument("--directory", default="artifacts/site"); parser.add_argument("--port", type=int, default=4173)
args = parser.parse_args()
class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".mjs": "text/javascript", ".webmanifest": "application/manifest+json"}
    def do_GET(self):
        if self.path.startswith("/VectorSpace/"):
            self.path = self.path[len("/VectorSpace"):]
        super().do_GET()
    def end_headers(self):
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()
handler = functools.partial(Handler, directory=args.directory)
server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), handler)
print(f"VectorSpace: http://127.0.0.1:{args.port}/VectorSpace/", flush=True)
server.serve_forever()
