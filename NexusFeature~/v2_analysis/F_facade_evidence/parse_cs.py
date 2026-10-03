import re,json,glob,sys,tiktoken
ED="/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity/Editor/"
V3={"oneOf":[{"type":"object","properties":{"x":{"type":"number"},"y":{"type":"number"},"z":{"type":"number"}}},{"type":"array","items":{"type":"number"},"minItems":3,"maxItems":3}]}
HELP={"GetVector3Schema":V3,
 "GetPrimitiveSchema":{"primitive_type":{"type":"string","enum":["Cube","Sphere","Capsule","Cylinder","Plane","Quad"]},"name":{"type":"string"},"parent_id":{"type":"integer"},"position":V3,"rotation":V3,"scale":V3,"material_path":{"type":"string"}},
 "GetSearchSchema":{"name":{"type":"string"},"tag":{"type":"string"},"type":{"type":"string"}},
 "GetTransformSchema":{"instance_id":{"type":"integer"},"position":V3,"rotation":V3,"scale":V3,"eulerAngles":V3,"localScale":V3}}
class P:
    def __init__(s,t): s.t=t; s.i=0
    def ws(s):
        while s.i<len(s.t):
            if s.t[s.i].isspace(): s.i+=1
            elif s.t.startswith("//",s.i): s.i=s.t.index("\n",s.i)
            else: break
    def peek(s,x): s.ws(); return s.t.startswith(x,s.i)
    def eat(s,x):
        s.ws()
        if not s.t.startswith(x,s.i): raise Exception(f"expected {x!r} at {s.t[s.i:s.i+80]!r}")
        s.i+=len(x)
    def string(s):
        s.ws(); verb=False
        if s.t[s.i]=='@': verb=True; s.i+=1
        s.eat('"'); out=""
        while True:
            c=s.t[s.i]
            if not verb and c=='\\':
                n=s.t[s.i+1]; out+={'n':'\n','t':'\t','"':'"','\\':'\\'}.get(n,n); s.i+=2; continue
            if c=='"':
                if verb and s.t[s.i+1]=='"': out+='"'; s.i+=2; continue
                s.i+=1; break
            out+=c; s.i+=1
        s.ws()
        if s.peek("+"): s.eat("+"); return out+s.string()
        return out
    def value(s):
        s.ws()
        if s.peek('"') or s.peek('@"'): return s.string()
        if s.peek("new JObject"):
            s.eat("new JObject"); o={}
            if s.peek("()"): s.eat("()")
            if not s.peek("{"): return o
            s.eat("{")
            while not s.peek("}"):
                s.eat("["); k=s.string(); s.eat("]"); s.eat("="); o[k]=s.value()
                if s.peek(","): s.eat(",")
            s.eat("}"); return o
        if s.peek("new JArray"):
            s.eat("new JArray"); a=[]
            if s.peek("{"): s.eat("{"); end="}"
            else: s.eat("("); end=")"
            while not s.peek(end):
                a.append(s.value())
                if s.peek(","): s.eat(",")
            s.eat(end); return a
        if s.peek("new[]") or s.peek("new string[]"):
            s.eat("new"); 
            while not s.peek("{"): s.i+=1
            s.eat("{"); a=[]
            while not s.peek("}"):
                a.append(s.value()); 
                if s.peek(","): s.eat(",")
            s.eat("}"); return a
        if s.peek("MaxBatchExecuteRequests"): s.eat("MaxBatchExecuteRequests"); return 50
        m=re.match(r"(Get\w+Schema)\(\)",s.t[s.i:])
        if m: s.i+=m.end(); return HELP[m.group(1)]
        m=re.match(r"-?\d+(\.\d+)?f?",s.t[s.i:])
        if m: s.i+=m.end(); v=m.group(0).rstrip('f'); return float(v) if '.' in v else int(v)
        for k,v in (("true",True),("false",False)):
            if s.peek(k): s.eat(k); return v
        raise Exception("unknown value at "+s.t[s.i:s.i+100])
tools=[];fails=[]
for f in sorted(glob.glob(ED+"MCPServerMethods*.cs")):
    txt=open(f).read()
    for m in re.finditer(r'CreateTool\("',txt):
        p=P(txt); p.i=m.end()-1
        try:
            name=p.string(); p.eat(","); desc=p.string(); p.eat(","); props=p.value(); req=[]
            while p.peek(","): p.eat(","); req.append(p.string())
            p.eat(")")
            sch={"type":"object","properties":props}
            if req: sch["required"]=req
            tools.append({"name":name,"description":desc,"inputSchema":sch})
        except Exception as e: fails.append((f,txt[m.start():m.start()+100],str(e)))
print("parsed",len(tools),"fails",len(fails))
for x in fails: print(x)
json.dump(tools,open(sys.argv[1],"w"),indent=1)
