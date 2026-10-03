import subprocess, json, sys, time, threading
args = sys.argv[1:]
p = subprocess.Popen(args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
out=[]
def rd():
    for line in p.stdout: out.append(line)
threading.Thread(target=rd, daemon=True).start()
def send(o): p.stdin.write(json.dumps(o)+"\n"); p.stdin.flush()
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"0"}}})
time.sleep(3)
send({"jsonrpc":"2.0","method":"notifications/initialized"})
send({"jsonrpc":"2.0","id":2,"method":"tools/list"})
time.sleep(8)
p.kill()
err = p.stderr.read()
sys.stdout.write("".join(out)); sys.stderr.write(err[:2000])
