from std.python import Python, PythonObject
from math import sqrt

fn spatial_cull(objects: PythonObject) raises -> String:
    var py = Python.import_module("builtins")
    var n = atol(String(py.len(objects)))
    if n == 0: return "Empty Scene"
    
    # Identify Camera first
    var cam_x: Float64 = 0.0
    var cam_y: Float64 = 0.0
    var cam_z: Float64 = 0.0
    var found_cam = False
    
    for i in range(n):
        var obj = objects[i]
        var name = String(obj[0])
        if "Camera" in name or "camera" in name:
            cam_x = atof(String(obj[2]))
            cam_y = atof(String(obj[3]))
            cam_z = atof(String(obj[4]))
            found_cam = True
            break
            
    var result = String("")
    var culled_count = 0
    
    for i in range(n):
        var obj = objects[i]
        var name = String(obj[0])
        var id = String(obj[1])
        var ox = atof(String(obj[2]))
        var oy = atof(String(obj[3]))
        var oz = atof(String(obj[4]))
        
        var dist: Float64 = 0.0
        if found_cam:
            var dx = ox - cam_x
            var dy = oy - cam_y
            var dz = oz - cam_z
            dist = sqrt(dx*dx + dy*dy + dz*dz)
        
        # Culling Logic (50m radius)
        if found_cam and dist > 50.0:
            if "Light" in name or "Sun" in name or "Camera" in name:
                result += "[" + id + "] " + name + " (Far) "
            else:
                culled_count += 1
                continue
        else:
            result += "[" + id + "] " + name + " "
            
    if culled_count > 0:
        result += "... and " + String(culled_count) + " distant objects culled."
        
    return result

fn mojo_http_post(url: String, body: String) raises -> String:
    var py = Python.import_module("builtins")
    var socket = Python.import_module("socket")
    
    # Parse URL (minimal for 127.0.0.1:port)
    var host = String("127.0.0.1")
    var port = 8081
    if "11434" in url: port = 11434
    
    var path = String("/")
    if "api/chat" in url: path = "/api/chat"
    
    var s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(60.0)
    
    var addr_list = py.list()
    addr_list.append(host)
    addr_list.append(port)
    var addr = py.tuple(addr_list)
    
    s.connect(addr)
    
    var http_req = "POST " + path + " HTTP/1.1\r\n"
    http_req += "Host: " + host + "\r\n"
    http_req += "Content-Type: application/json\r\n"
    http_req += "Content-Length: " + String(len(body)) + "\r\n"
    http_req += "Connection: close\r\n\r\n"
    http_req += body
    
    s.sendall(py.str(http_req).encode("utf-8"))
    
    var response_py = py.str("")
    while True:
        var chunk = s.recv(4096)
        if py.len(chunk) == 0: break
        response_py = response_py + chunk.decode("utf-8", "ignore")
        
    s.close()
    
    # Extract Body using Python slicing to bypass Mojo String slicing ambiguity
    var response_str = String(response_py)
    var body_idx = response_str.find("\r\n\r\n")
    if body_idx == -1: return response_str
    
    var final_body = response_py[body_idx + 4:]
    return String(final_body)

fn mojo_http_get_check(url: String) raises -> Bool:
    var py = Python.import_module("builtins")
    var socket = Python.import_module("socket")
    var host = String("127.0.0.1")
    var port = 11434
    
    var s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(2.0)
    var addr_list = py.list()
    addr_list.append(host)
    addr_list.append(port)
    var addr = py.tuple(addr_list)
    
    try:
        s.connect(addr)
        s.close()
        return True
    except:
        return False

def main() raises:
    var py = Python.import_module("builtins")
    var sys = Python.import_module("sys")
    var json = Python.import_module("json")
    var traceback = Python.import_module("traceback")
    var os = Python.import_module("os")
    var subprocess = Python.import_module("subprocess")
    var time = Python.import_module("time")
    
    var unity_url = "http://127.0.0.1:8081/"
    var llm_url = "http://127.0.0.1:11434/api/chat"
    var parent_pid = os.getppid()

    # Define Zero-Copy Scanner in Python Globals
    var scanner_script = py.str("""import urllib.request, json
def get_scene_data(unity_url):
    payload = {'jsonrpc': '2.0', 'method': 'dump_scene_graph', 'params': {'max_depth': 2}, 'id': 100}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(unity_url, data=data, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=10) as f:
            data = f.read()
    except:
        return []
    L = len(data)
    results = []
    idx = 0
    t_name = b'"name":'
    t_id = b'"instance_id":'
    t_pos = b'"position":'
    while idx < L:
        i_name = data.find(t_name, idx)
        if i_name == -1: break
        s_name = i_name + 7
        while s_name < L and (data[s_name] == 32 or data[s_name] == 34): s_name += 1
        e_name = data.find(b'"', s_name)
        if e_name == -1: break
        name = data[s_name:e_name].decode('utf-8', errors='ignore')
        i_id = data.find(t_id, e_name)
        if i_id == -1: break
        s_id = i_id + 14
        e_id1 = data.find(b',', s_id)
        e_id2 = data.find(b'}', s_id)
        e_id = min(e_id1 if e_id1 != -1 else L, e_id2 if e_id2 != -1 else L)
        obj_id = data[s_id:e_id].strip().decode('utf-8', errors='ignore')
        i_pos = data.find(t_pos, e_id)
        if i_pos == -1 or i_pos - e_id > 1000:
            results.append((name, obj_id, 0.0, 0.0, 0.0))
            idx = e_id
            continue
        try:
            i_x = data.find(b'"x":', i_pos)
            s_x = i_x + 4
            e_x = data.find(b',', s_x)
            val_x = float(data[s_x:e_x])
            i_y = data.find(b'"y":', e_x)
            s_y = i_y + 4
            e_y = data.find(b',', s_y)
            val_y = float(data[s_y:e_y])
            i_z = data.find(b'"z":', e_y)
            s_z = i_z + 4
            e_z = data.find(b'}', s_z)
            val_z = float(data[s_z:e_z])
            results.append((name, obj_id, val_x, val_y, val_z))
            idx = e_z
        except:
            results.append((name, obj_id, 0.0, 0.0, 0.0))
            idx = i_pos + 10
    return results
""")
    var py_globals = py.dict()
    py.getattr(py, "exec")(scanner_script, py_globals)
    var get_scene_data = py_globals.get("get_scene_data")

    sys.stderr.write(py.str("DEBUG: NexusUnity Mojo Stage 6 Active (Native Socket Transport + Spatial Culling)\n"))
    sys.stderr.flush()

    # Auto-Start Ollama if it's down
    var started_ollama = False
    var ollama_process = py.None

    # Start Background Worker for Log Streaming and Heartbeat
    var bg_worker_code = py.str("""import os, sys, time, json, urllib.request
main_pid = int(sys.argv[1])
port = int(sys.argv[2])
unity_url = f'http://127.0.0.1:{port}/'

def call_unity(method, params=None):
    payload = {'jsonrpc': '2.0', 'method': method, 'params': params or {}, 'id': 1}
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(unity_url, data=data, headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=1) as f:
            return json.loads(f.read().decode('utf-8'))
    except:
        return None

cursor = 0
res = call_unity('read_logs_since_cursor', {'cursor': 0})
if res and 'result' in res and 'new_cursor' in res['result']:
    cursor = res['result']['new_cursor']

while True:
    try:
        os.kill(main_pid, 0)
    except:
        sys.exit(0)
    try:
        res = call_unity('read_logs_since_cursor', {'cursor': cursor})
        if res and 'result' in res:
            logs = res['result'].get('logs', [])
            for l in logs:
                msg = l.get('Message', '').strip()
                log_type = l.get('Type', 'Log')
                if msg:
                    sys.stderr.write(f'[Unity {log_type}] {msg}\\n')
            if 'new_cursor' in res['result']:
                cursor = res['result']['new_cursor']
            sys.stderr.flush()
    except:
        pass
    time.sleep(1.0)
""")
    var bg_args = py.list()
    bg_args.append(py.str("python3"))
    bg_args.append(py.str("-c"))
    bg_args.append(bg_worker_code)
    bg_args.append(py.str(os.getpid()))
    bg_args.append(py.str("8081"))
    var bg_process = subprocess.Popen(bg_args)

    if not mojo_http_get_check(String("http://127.0.0.1:11434/")):
        sys.stderr.write(py.str("DEBUG: Ollama service is down. Auto-starting in background...\n"))
        sys.stderr.flush()
        started_ollama = True
        ollama_process = subprocess.Popen(py.str("ollama serve"), shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        time.sleep(2.5)

    while True:
        var line = sys.stdin.readline()
        if not line: break
        
        # Track current request ID for error recovery
        var current_req_id = py.None

        try:
            var request = json.loads(line)
            var method = request.get("method")
            var req_id = request.get("id")
            current_req_id = req_id
            
            if method == "initialize":
                var res = py.dict()
                res["protocolVersion"] = py.str("2024-11-05")
                var serverInfo = py.dict()
                serverInfo["name"] = py.str("NexusUnity-Mojo-S6")
                serverInfo["version"] = py.str("3.0.0-mojo")
                res["serverInfo"] = serverInfo
                res["capabilities"] = py.dict()
                var response = py.dict()
                response["jsonrpc"] = py.str("2.0")
                response["id"] = req_id
                response["result"] = res
                sys.stdout.write(py.str(json.dumps(response)) + py.str("\n"))
                sys.stdout.flush()
            
            elif method == "tools/list":
                var result = py.dict()
                result["tools"] = py.list()
                var s2_tool = json.loads(py.str('{"name": "unity_semantic_find", "description": "Search scene using Llama 3.2 3B high-efficiency intelligence. Optimized for structural scene analysis.", "inputSchema": {"type": "object", "properties": {"query": {"type": "string"}}, "required": ["query"]}}'))
                result["tools"].append(s2_tool)
                var response = py.dict()
                response["jsonrpc"] = py.str("2.0")
                response["id"] = req_id
                response["result"] = result
                sys.stdout.write(py.str(json.dumps(response)) + py.str("\n"))
                sys.stdout.flush()

            elif method == "tools/call":
                var params = request.get("params")
                var name_py = py.str(params.get("name"))
                var name = String(name_py).replace("unity_", "")
                var args = params.get("arguments")
                
                if name == "semantic_find":
                    sys.stderr.write(py.str("DEBUG: [SemanticFind] Fetching & Scanning Scene (Zero-Copy)...\n"))
                    sys.stderr.flush()
                    
                    var time_start = Python.import_module("time").time()
                    var scene_data = get_scene_data(py.str(unity_url))
                    var scene_summary = spatial_cull(scene_data)
                    var time_end = Python.import_module("time").time()
                    
                    sys.stderr.write(py.str("DEBUG: [SemanticFind] Zero-Copy + Spatial Culling Time: ") + py.str(time_end - time_start) + py.str("s\n"))
                    sys.stderr.write(py.str("DEBUG: [SemanticFind] Calling Llama 3.2 3B Reasoning...\n"))
                    sys.stderr.flush()
                    
                    var system_prompt = py.str("You are Nexus AI, a Unity engine expert. Analyze the scene graph summary to find the requested objects. Return only the instance_ids in a comma-separated list, or a brief explanation if not found.")
                    
                    var query_py = py.str(args.get("query"))
                    var combined_prompt = py.str("Scene Summary: ") + py.str(scene_summary) + py.str("\nQuery: ") + query_py
                    
                    var llm_payload = py.dict()
                    llm_payload["model"] = py.str("llama3.2:3b")
                    llm_payload["stream"] = False
                    
                    var messages = py.list()
                    var msg_sys = py.dict()
                    msg_sys["role"] = py.str("system")
                    msg_sys["content"] = system_prompt
                    messages.append(msg_sys)
                    
                    var msg_user = py.dict()
                    msg_user["role"] = py.str("user")
                    msg_user["content"] = combined_prompt
                    messages.append(msg_user)
                    
                    llm_payload["messages"] = messages
                    
                    sys.stderr.write(py.str("DEBUG: [SemanticFind] Sending to Ollama via Native Socket...\n"))
                    sys.stderr.flush()
                    
                    try:
                        var res_body = mojo_http_post(llm_url, String(json.dumps(llm_payload)))
                        var ai_json = json.loads(py.str(res_body))
                        var content = py.str("AI failed to respond")
                        
                        var msg_obj = py.getattr(ai_json, "get")(py.str("message"))
                        if py.bool(msg_obj):
                            content = py.getattr(msg_obj, "get")(py.str("content"), py.str("Empty"))
                        
                        var response_call = py.dict()
                        response_call["jsonrpc"] = py.str("2.0")
                        response_call["id"] = req_id
                        var content_list = py.list()
                        var content_obj = py.dict()
                        content_obj["type"] = py.str("text")
                        content_obj["text"] = content
                        content_list.append(content_obj)
                        var res_obj = py.dict()
                        res_obj["content"] = content_list
                        response_call["result"] = res_obj
                        sys.stdout.write(py.str(json.dumps(response_call)) + py.str("\n"))
                        sys.stdout.flush()
                    except e:
                        sys.stderr.write(py.str("DEBUG_LLM_ERR: ") + py.str(String(e)) + py.str("\n"))
                        sys.stderr.flush()
                        raise Error("Ollama request failed: " + String(e))
                else:
                    var payload = py.dict()
                    payload["jsonrpc"] = py.str("2.0")
                    payload["method"] = py.str(name)
                    payload["params"] = args
                    payload["id"] = 1
                    
                    var res_body = mojo_http_post(unity_url, String(json.dumps(payload)))
                    var unity_res = json.loads(py.str(res_body))
                    
                    var response_call = py.dict()
                    response_call["jsonrpc"] = py.str("2.0")
                    response_call["id"] = req_id
                    var content_list = py.list()
                    var content_obj = py.dict()
                    content_obj["type"] = py.str("text")
                    content_obj["text"] = json.dumps(unity_res.get("result"))
                    content_list.append(content_obj)
                    var res_obj = py.dict()
                    res_obj["content"] = content_list
                    response_call["result"] = res_obj
                    sys.stdout.write(py.str(json.dumps(response_call)) + py.str("\n"))
                    sys.stdout.flush()

            if os.getppid() != parent_pid or os.getppid() == 1:
                if started_ollama and py.bool(ollama_process):
                    ollama_process.terminate()
                bg_process.terminate()
                os._exit(0)

        except e:
            sys.stderr.write(py.str("DEBUG_ERR: ") + py.str(String(e)) + py.str("\n"))
            sys.stderr.flush()
            
            # RECOVERY: Always send a response so the client isn't stuck
            var err_response = py.dict()
            err_response["jsonrpc"] = py.str("2.0")
            err_response["id"] = current_req_id
            var err_obj = py.dict()
            err_obj["code"] = -32000
            err_obj["message"] = py.str("Internal Mojo Bridge Error: ") + py.str(String(e))
            err_response["error"] = err_obj
            sys.stdout.write(py.str(json.dumps(err_response)) + py.str("\n"))
            sys.stdout.flush()

    if started_ollama and py.bool(ollama_process):
        ollama_process.terminate()
    bg_process.terminate()
