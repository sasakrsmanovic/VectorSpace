"""Black-box tests against the actual ASP.NET process; no in-memory transport substitute."""
import concurrent.futures
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import unittest
import urllib.error
import urllib.request
import uuid

class CollaborationServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory = tempfile.TemporaryDirectory()
        cls.key = secrets.token_urlsafe(48)
        with socket.socket() as s:
            s.bind(('127.0.0.1', 0)); cls.port = s.getsockname()[1]
        cls.base = f'http://127.0.0.1:{cls.port}'
        Path('artifacts').mkdir(exist_ok=True)
        cls.log = open('artifacts/collaboration-server.log', 'w')
        cls.start()

    @classmethod
    def start(cls):
        env = dict(os.environ, VECTORSPACE_CREATE_KEY=cls.key, VECTORSPACE_DATA=cls.directory.name)
        cls.process = subprocess.Popen(['dotnet', 'server/VectorSpace.Server/bin/Release/net10.0/VectorSpace.Server.dll', '--urls', cls.base], env=env, stdout=cls.log, stderr=subprocess.STDOUT)
        for _ in range(150):
            if cls.process.poll() is not None: raise RuntimeError('Collaboration service exited; inspect artifacts/collaboration-server.log')
            try:
                urllib.request.urlopen(cls.base + '/health', timeout=1).close(); return
            except (OSError, urllib.error.URLError): time.sleep(.2)
        raise TimeoutError('Server did not start')

    @classmethod
    def stop(cls):
        cls.process.terminate()
        try: cls.process.wait(8)
        except subprocess.TimeoutExpired: cls.process.kill(); cls.process.wait()

    @classmethod
    def tearDownClass(cls):
        cls.stop(); cls.log.close(); cls.directory.cleanup()

    def request(self, path, data=None, token=None, method=None, create=False):
        headers = {}
        if token: headers['Authorization'] = 'Bearer ' + token
        if create: headers['X-VectorSpace-Create-Key'] = self.key
        body = None if data is None else json.dumps(data).encode()
        if body: headers['Content-Type'] = 'application/json'
        req = urllib.request.Request(self.base + path, body, headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=25) as response:
                text = response.read().decode()
                return response.status, json.loads(text) if text.startswith(('{', '[')) else text
        except urllib.error.HTTPError as error:
            return error.code, error.read().decode()

    def room(self):
        doc = {'formatVersion': 4, 'id': 'doc', 'name': 'Network test', 'pages': [{'id': 'page', 'nodes': [{'id': 'a', 'name': 'A'}]}]}
        status, invite = self.request('/api/rooms', {'document': json.dumps(doc), 'name': 'Owner'}, create=True)
        self.assertEqual(status, 200)
        return '/api/rooms/' + invite['roomId'], invite['token']

    def snapshot(self, path, token):
        status, value = self.request(path + '/sync?since=-1', token=token)
        self.assertEqual(status, 200); return value['snapshot']

    def batch(self, snapshot, prop='x', value='10', client=None, sequence=1):
        key = 'node:a\x1f' + prop
        return {'id': uuid.uuid4().hex, 'clientId': client or uuid.uuid4().hex, 'sequence': sequence, 'label': 'Move layer', 'baseRevision': snapshot['revision'],
                'changes': [{'key': key, 'before': snapshot['cells'][key], 'after': value, 'expectedVersion': snapshot['versions'].get(key, 0)}]}

    def test_creation_requires_administrator_key(self):
        status, _ = self.request('/api/rooms', {'document': '{}', 'name': 'Denied'})
        self.assertEqual(status, 403)

    def test_invalid_and_revoked_invitation_cannot_read(self):
        path, token = self.room()
        self.assertEqual(self.request(path + '/sync?since=-1', token='invalid' * 8)[0], 403)
        status, guest = self.request(path + '/invitations', {'label': 'Review', 'role': 'Viewer'}, token)
        self.assertEqual(status, 200)
        self.assertEqual(self.request(path + '/sync?since=-1', token=guest['token'])[0], 200)
        self.assertEqual(self.request(path + '/invitations/' + guest['grantId'], token=token, method='DELETE')[0], 200)
        self.assertEqual(self.request(path + '/sync?since=-1', token=guest['token'])[0], 403)

    def test_viewer_cannot_write_or_create_edit_invites(self):
        path, token = self.room(); snapshot = self.snapshot(path, token)
        _, guest = self.request(path + '/invitations', {'label': 'Viewer', 'role': 'Viewer'}, token)
        status, result = self.request(path + '/edits', self.batch(snapshot), guest['token'])
        self.assertEqual(status, 200); self.assertFalse(result['receipt']['accepted'])
        self.assertEqual(self.snapshot(path, token)['revision'], 0)
        self.assertEqual(self.request(path + '/invitations', {'label': 'Escalate', 'role': 'Editor'}, guest['token'])[0], 403)

    def test_concurrent_clients_merge_disjoint_properties(self):
        path, token = self.room(); snapshot = self.snapshot(path, token)
        batches = [self.batch(snapshot, 'x', '20'), self.batch(snapshot, 'y', '30')]
        with concurrent.futures.ThreadPoolExecutor() as pool:
            results = list(pool.map(lambda b: self.request(path + '/edits', b, token), batches))
        self.assertTrue(all(r[0] == 200 and r[1]['receipt']['accepted'] for r in results))
        current = self.snapshot(path, token)
        self.assertEqual(current['revision'], 2); self.assertEqual(current['cells']['node:a\x1fx'], '20'); self.assertEqual(current['cells']['node:a\x1fy'], '30')

    def test_conflicts_are_atomic_and_retry_is_idempotent(self):
        path, token = self.room(); snapshot = self.snapshot(path, token); batch = self.batch(snapshot)
        _, result = self.request(path + '/edits', batch, token); self.assertTrue(result['receipt']['accepted'])
        _, repeat = self.request(path + '/edits', batch, token); self.assertTrue(repeat['receipt']['accepted']); self.assertEqual(repeat['revision'], 1)
        conflict = self.batch(snapshot, value='90'); conflict['changes'] += self.batch(snapshot, 'y', '60')['changes']
        _, result = self.request(path + '/edits', conflict, token); self.assertFalse(result['receipt']['accepted'])
        self.assertEqual(self.snapshot(path, token)['cells']['node:a\x1fy'], '0')

    def test_long_poll_wakes_for_committed_delta_without_full_document(self):
        path, token = self.room(); snapshot = self.snapshot(path, token)
        _, initial = self.request(path + '/sync?since=0', token=token)
        with concurrent.futures.ThreadPoolExecutor() as pool:
            waiting = pool.submit(self.request, path + f'/sync?since=0&afterEvent={initial["event"]}&wait=true', None, token)
            time.sleep(.1); self.request(path + '/edits', self.batch(snapshot), token)
            status, reply = waiting.result(5)
        self.assertEqual(status, 200); self.assertNotIn('snapshot', reply); self.assertEqual(len(reply['commits']), 1)
        self.assertEqual(len(reply['commits'][0]['changes']), 1)

    def test_presence_is_ephemeral_and_departure_removes_it(self):
        path, token = self.room(); client = uuid.uuid4().hex
        presence = dict(clientId=client, name='Alice', color='#000000', pageId='page', x=15, y=20, hasCursor=True, zoom=1, panX=0, panY=0, selection=['a'], seenAt='2000-01-01T00:00:00Z')
        self.assertEqual(self.request(path + '/presence', presence, token)[0], 200)
        _, state = self.request(path + '/sync?since=0', token=token)
        self.assertEqual(state['revision'], 0); self.assertEqual(state['participants'][0]['name'], 'Alice'); self.assertNotEqual(state['participants'][0]['color'], '#000000')
        self.request(path + '/presence/' + client, token=token, method='DELETE')
        self.assertEqual(self.request(path + '/sync?since=0', token=token)[1]['participants'], [])

    def test_restart_recovers_commits_receipts_and_truncated_tail(self):
        path, token = self.room(); batch = self.batch(self.snapshot(path, token)); self.request(path + '/edits', batch, token)
        self.stop()
        journal = Path(self.directory.name) / (path.rsplit('/', 1)[1] + '.room.journal')
        with journal.open('ab') as stream: stream.write(b'\x12\x13\x14')
        self.start()
        _, reply = self.request(path + '/edits', batch, token)
        self.assertTrue(reply['receipt']['accepted']); self.assertEqual(reply['revision'], 1)
        _, old = self.request(path + '/versions/0', token=token); _, current = self.request(path + '/versions/1', token=token)
        self.assertEqual(old['pages'][0]['nodes'][0]['x'], 0); self.assertEqual(current['pages'][0]['nodes'][0]['x'], 10)
        self.assertEqual(len(self.request(path + '/history', token=token)[1]), 1)

    def test_schema_four_rooms_upgrade_once_without_rewriting_old_history(self):
        path, token = self.room()
        self.stop()
        metadata = Path(self.directory.name) / (path.rsplit('/', 1)[1] + '.room.json')
        data = json.loads(metadata.read_text())
        data['initial']['cells']['$root\x1fformatVersion'] = '4'
        metadata.write_text(json.dumps(data))
        self.start()
        current = self.snapshot(path, token)
        self.assertEqual(current['cells']['$root\x1fformatVersion'], '6')
        self.assertEqual(current['revision'], 1)
        self.assertEqual(self.request(path + '/history', token=token)[1][0]['author'], 'System')
        self.stop(); self.start()
        self.assertEqual(self.snapshot(path, token)['revision'], 1)
        batch = self.batch(self.snapshot(path, token), value='27')
        self.assertTrue(self.request(path + '/edits', batch, token)[1]['receipt']['accepted'])
        self.assertEqual(self.request(path + '/versions/0', token=token)[1]['pages'][0]['nodes'][0]['x'], 0)

    def test_schema_five_strokes_upgrade_and_remain_editable_after_restart(self):
        path, token = self.room()
        self.stop()
        metadata = Path(self.directory.name) / (path.rsplit('/', 1)[1] + '.room.json')
        data = json.loads(metadata.read_text())
        data['initial']['cells']['$root\x1fformatVersion'] = '5'
        data['initial']['cells']['node:a\x1fstrokes'] = '[{"width":4}]'
        metadata.write_text(json.dumps(data))
        self.start()
        current = self.snapshot(path, token)
        self.assertEqual(current['revision'], 1)
        self.assertEqual(current['cells']['$root\x1fformatVersion'], '6')
        stroke = json.loads(current['cells']['node:a\x1fstrokes'])[0]
        self.assertEqual(stroke['cap'], 'Round')
        self.assertEqual(stroke['alignment'], 'Center')
        stroke['alignment'] = 'Outside'
        batch = self.batch(current, 'strokes', json.dumps([stroke]))
        self.assertTrue(self.request(path + '/edits', batch, token)[1]['receipt']['accepted'])
        self.stop(); self.start()
        restored = self.snapshot(path, token)
        self.assertEqual(restored['revision'], 2)
        self.assertEqual(json.loads(restored['cells']['node:a\x1fstrokes'])[0]['alignment'], 'Outside')
        historic = self.request(path + '/versions/0', token=token)[1]
        self.assertEqual(historic['pages'][0]['nodes'][0]['strokes'][0]['alignment'], 'Center')

if __name__ == '__main__': unittest.main(verbosity=2)
