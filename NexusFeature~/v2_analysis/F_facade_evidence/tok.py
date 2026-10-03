import json,tiktoken,collections
enc=tiktoken.get_encoding("cl100k_base"); T=lambda s: len(enc.encode(s if isinstance(s,str) else json.dumps(s,separators=(",",":"))))
lines=[json.loads(l) for l in open("mcp_editor.jsonl")]
tl=[l for l in lines if l.get("id")==2][0]["result"]["tools"]
print("unity mcp tools:",len(tl),"tokens(all tools json):",T(tl))
print("names+desc only:",T([{"name":t["name"],"description":t.get("description","")} for t in tl]))
print("names only:",T(" ".join(t["name"] for t in tl)))
per=sorted(((T(t),t["name"]) for t in tl),reverse=True); print("top10",per[:10]); print("median",per[len(per)//2])
L=json.load(open("unity_list.json"))["data"]["tools"]
g=collections.Counter(t["group"] for t in L); print("groups",g)
print("unity list count",len(L),"human-list tokens",T(open("unity_list_human.txt").read()))
print("mcp names not in list", set(t["name"] for t in tl)-set(t["name"] for t in L))
print(json.dumps(tl[0])[:800])
