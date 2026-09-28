#!/usr/bin/env python3
"""Start a real temporary backend, run Playwright, and always stop the service.

Requires a compiled server and a separately served published Uno application.
No request interception or JavaScript document-mutation API substitutes for the backend.
"""
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request


def main():
    assembly = Path('server/VectorSpace.Server/bin/Release/net10.0/VectorSpace.Server.dll')
    if not assembly.is_file():
        raise SystemExit('Compile server/VectorSpace.Server in Release before running this script.')
    with socket.socket() as endpoint:
        endpoint.bind(('127.0.0.1', 0))
        port = endpoint.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    Path('artifacts').mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='vectorspace-browser-rooms-') as data, open('artifacts/collaboration-browser-server.log', 'w') as log:
        env = dict(os.environ, VECTORSPACE_CREATE_KEY=secrets.token_urlsafe(48), VECTORSPACE_DATA=data, VECTORSPACE_COLLAB_URL=base)
        process = subprocess.Popen(['dotnet', str(assembly), '--urls', base], env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(150):
                if process.poll() is not None:
                    raise RuntimeError('Collaboration service exited during startup; inspect its retained log.')
                try:
                    urllib.request.urlopen(base + '/health', timeout=1).close()
                    break
                except (OSError, urllib.error.URLError):
                    time.sleep(.2)
            else:
                raise TimeoutError('Collaboration service did not become ready.')
            arguments = sys.argv[1:]
            if arguments[:1] == ['--']: arguments = arguments[1:]
            print('Running browser acceptance with an isolated real collaboration server.', flush=True)
            return subprocess.run(['npx', 'playwright', 'test', *arguments], env=env, check=False).returncode
        finally:
            process.terminate()
            try: process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait()


if __name__ == '__main__':
    sys.exit(main())
