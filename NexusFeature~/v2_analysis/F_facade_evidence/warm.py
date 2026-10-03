import subprocess,json,time,statistics,sys
P="/Users/daliys/Daliys/UnityProjects/UnityTestForNexus"
p=subprocess.Popen(["unity","mcp","--project-path",P],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True)
i=0
def call(m,params=None):
    global i; i+=1; p.stdin.write(json.dumps({"jsonrpc":"2.0","id":i,"method":m,"params":params or {}})+"\n"); p.stdin.flush()
    while True:
        l=json.loads(p.stdout.readline())
        if l.get("id")==i: return l
call("initialize",{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}})
p.stdin.write(json.dumps({"jsonrpc":"2.0","method":"notifications/initialized"})+"\n"); p.stdin.flush()
call("tools/list")
for name,args,n in [("editor_status",{},20),("get_scene_hierarchy",{},5)]:
    xs=[]
    for _ in range(n):
        a=time.perf_counter(); r=call("tools/call",{"name":name,"arguments":args}); xs.append((time.perf_counter()-a)*1000)
    print(name,"warm mcp ms median/min/max",round(statistics.median(xs)),round(min(xs)),round(max(xs)), "resp bytes",len(json.dumps(r)))
raw=r
a=time.perf_counter()
import compact
p.kill()
