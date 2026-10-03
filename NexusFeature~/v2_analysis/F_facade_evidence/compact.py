# ponytail: throwaway research normalizer to size a façade's output; not production code.
import json,re,tiktoken,collections
enc=tiktoken.get_encoding("cl100k_base"); T=lambda s: len(enc.encode(s if isinstance(s,str) else json.dumps(s,separators=(",",":"))))
R=lambda n: json.load(open(f"raw/{n}.json"))["data"]["result"]
rows=[]
def rec(name,raw_json,raw_human,compact,note):
    rows.append((name,T(open(raw_json).read()),T(open(raw_human).read()),T(compact),note)); return compact
# hierarchy: depth<=2 tree, collapse same-shape siblings, component lists only where non-trivial
h=R("hier")
def shape(n): return (re.sub(r"_\d+$","_#",n["name"]),tuple(n["components"]),len(n["children"]))
def node(n,d,maxd):
    o={"n":n["name"]}
    if not n["activeSelf"]: o["off"]=1
    comps=[c for c in n["components"] if c not in("Transform","RectTransform","CanvasRenderer")]
    if comps: o["c"]=comps
    total=sum(1 for _ in walk(n))-1
    if n["children"]:
        if d>=maxd: o["kids"]=total
        else:
            groups=collections.OrderedDict()
            for c in n["children"]: groups.setdefault(shape(c),[]).append(c)
            ch=[]
            for k,g in groups.items():
                x=node(g[0],d+1,maxd)
                if len(g)>1: x["n"]=k[0]; x["x"]=len(g); x["off_n"]=sum(1 for c in g if not c["activeSelf"]) or None
                ch.append({a:b for a,b in x.items() if b is not None})
            o["ch"]=ch
    return o
def walk(n):
    yield n
    for c in n["children"]: yield from walk(c)
total=sum(sum(1 for _ in walk(r)) for r in h["roots"])
roots=collections.OrderedDict()
for r in h["roots"]: roots.setdefault(shape(r),[]).append(r)
out=[]
for k,g in roots.items():
    x=node(g[0],0,2)
    if len(g)>1: x["n"]=k[0]; x["x"]=len(g)
    out.append(x)
hc={"objects":total,"roots":len(h["roots"]),"tree":out,"more":"snap://h1 (depth>2, cursor)"}
rec("hierarchy (~1.1k objs)","raw/hier.json","raw/hier.human",hc,"depth 2 + sibling-shape collapse + handle")
# console: group by message signature
c=R("console")
grp=collections.OrderedDict()
for e in c["entries"]:
    key=(e["level"],e["message"].split("\n")[0][:160]); g=grp.setdefault(key,{"lvl":e["level"],"msg":key[1],"n":0}); g["n"]+=1
    m=re.search(r"\(at ([^)]+:\d+)\)",e["message"]+e.get("stackTrace","")); 
    if m and "at" not in g: g["at"]=m.group(1)
cc={"counts":c["counts"],"groups":sorted(grp.values(),key=lambda g:(g["lvl"]!="error",-g["n"]))[:5],"cursor":c["cursor"]}
rec("console tail 100 (47 entries)","raw/console.json","raw/console.human",cc,"dedupe by first line, top-5 groups, counts, cursor")
ce=R("console_err")
grp={}
for e in ce["entries"]:
    k=e["message"].split("\n")[0]; grp.setdefault(k,{"msg":k,"n":0,"at":re.search(r"\(at ([^)]+:\d+)\)",e["message"]).group(1) if "(at " in e["message"] else None})["n"]+=1
rec("console level=error (6 dup errors)","raw/console_err.json","raw/console_err.human",{"errors":list(grp.values())},"dedupe")
f=R("find")
paths=[g.get("hierarchyPath") or g.get("path") or g.get("name") for g in f["gameObjects"]]
rec("find_gameobjects name=Button_0 (120 hits)","raw/find.json","raw/find.human",{"count":f["count"],"top":paths[:10],"more":"cursor"},"top-10 paths + count + cursor")
cp=R("comp")
def clean(o):
    if isinstance(o,dict): return {k:clean(v) for k,v in o.items() if v not in (None,[],{},"") and k not in("globalId","guid","assetPath","instanceId")}
    if isinstance(o,list): return [clean(v) for v in o]
    return o
rec("get_component_properties Button","raw/comp.json","raw/comp.human",clean(cp),"drop null/ids")
s=R("ser"); rec("get_serialized_fields Image","raw/ser.json","raw/ser.human",clean(s),"drop null/ids")
print("| Output | raw JSON tok | raw human tok | compact tok | reduction vs human | method |\n|---|---|---|---|---|---|")
for n,a,b,cpt,note in rows: print(f"| {n} | {a} | {b} | {cpt} | {100-100*cpt/b:.0f}% | {note} |")
json.dump({"hier":hc,"console":cc},open("compact_samples.json","w"),indent=1)
