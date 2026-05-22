import sys
import json
import urllib.request
import os

DEFAULT_PORT = 8081


def _normalize_url(url):
    return url if url.endswith("/") else url + "/"


def _read_port():
    raw_port = os.environ.get("NEXUS_UNITY_PORT")
    if raw_port:
        return int(raw_port)

    if len(sys.argv) > 1:
        try:
            return int(sys.argv[1])
        except ValueError:
            pass

    return DEFAULT_PORT


def _read_timeout():
    raw_timeout = os.environ.get("NEXUS_UNITY_TIMEOUT_SECONDS")
    if not raw_timeout:
        return 120

    return max(1, float(raw_timeout))


UNITY_URL = _normalize_url(os.environ["NEXUS_UNITY_URL"]) if os.environ.get("NEXUS_UNITY_URL") else f"http://127.0.0.1:{_read_port()}/"
UNITY_TIMEOUT_SECONDS = _read_timeout()

def log(msg):
    sys.stderr.write(f"DEBUG: {msg}\n")
    sys.stderr.flush()

def call_unity(method, params=None):
    payload = {"jsonrpc": "2.0", "method": method, "params": params or {}, "id": 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(UNITY_URL, data=data, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=UNITY_TIMEOUT_SECONDS) as f:
            return json.loads(f.read().decode('utf-8'))
    except Exception as e:
        return {"error": {"code": -32000, "message": f"Unity Server unreachable. Error: {str(e)}"}}
