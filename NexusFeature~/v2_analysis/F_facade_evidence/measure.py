import json,tiktoken,re
enc=tiktoken.get_encoding("cl100k_base"); T=lambda o:len(enc.encode(json.dumps(o)))
cs=json.load(open("cs_list_tools.json"))
canon=[{"name":"nexus.project_map","description":"Generates a curated high-level project intelligence map for AI agents.","inputSchema":{"type":"object","properties":{}}},
{"name":"nexus.group_compile_errors","description":"Analyzes recent console logs, extracts C# compiler errors (CSxxxx), and groups them by root cause file.","inputSchema":{"type":"object","properties":{"max_logs":{"type":"integer","description":"Maximum number of recent console log entries to inspect."}}}},
{"name":"nexus.capture_game_view","description":"Captures presented Game View pixels including Overlay UI, with JPEG/PNG and optional downscale.","inputSchema":{"type":"object","properties":{p:{"type":t,"description":d} for p,t,d in [("width","integer","Target width in pixels (0 for native Game View width)."),("height","integer","Target height in pixels (0 for native Game View height)."),("format","string","Image encoding format: 'jpg' (default) or 'png'."),("quality","integer","JPEG compression quality (1-100, default 85)."),("max_dimension","integer","Maximum allowed longest edge in pixels (0 for unconstrained).")]}}}]
allc=cs+canon
names=[t["name"] for t in allc]
print("C# list_tools entries",len(allc),"unique",len(set(names)))
resp=lambda tools:{"jsonrpc":"2.0","id":1,"result":{"tools":tools}}
print("C# raw (no prefix) resp tokens",T(resp(allc)), "per tool",round(T(resp(allc))/len(allc),1))
pref=[dict(t,name="unity_"+t["name"].replace("nexus.","nexus_")) for t in allc]
print("prefixed resp", T(resp(pref)))
empties=sum(1 for t in allc if not t["inputSchema"]["properties"]); print("tools w/o params",empties)
nd=[t for t in allc if len(t["description"])<25]; print("short desc(<25ch)",len(nd))
# distribution
sz=sorted((T(t),t["name"]) for t in allc); print("min",sz[:3],"max",sz[-5:]); import statistics; print("median",statistics.median([s for s,_ in sz]))
json.dump(resp(pref),open("atomic_tools_list.json","w"))
for f in ["funplay","ivan"]:
    d=json.load(open(f"/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity/NexusFeature~/results/{f}_tools_list.json"))
    tools=d.get("result",d).get("tools") if isinstance(d,dict) else d
    if tools is None: print(f,list(d.keys())); continue
    raw=open(f"/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity/NexusFeature~/results/{f}_tools_list.json").read()
    print(f,"tools",len(tools),"file tokens",len(enc.encode(raw)),"json.dumps tools tokens",T({"tools":tools}),"per tool",round(T({"tools":tools})/len(tools),1), "compact",len(enc.encode(json.dumps({"tools":tools},separators=(',',':')))))
