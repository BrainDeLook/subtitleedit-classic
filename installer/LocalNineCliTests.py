import json, subprocess, threading, http.server, pathlib, sys, tempfile
exe = pathlib.Path(sys.argv[1]).resolve()
root = pathlib.Path(sys.argv[2]).resolve(); root.mkdir(parents=True,exist_ok=True); root=pathlib.Path(tempfile.mkdtemp(prefix='run-',dir=root))
source=root/'sample.srt';source.write_text('1\n00:00:01,000 --> 00:00:02,000\nHello\n\n2\n00:00:02,100 --> 00:00:03,200\nWorld\n',encoding='utf-8')
checks=0
requests=[]
class Handler(http.server.BaseHTTPRequestHandler):
 def log_message(self,*args): pass
 def do_POST(self):
  body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
  requests.append(body)
  target='fr' if ' to fr' in body['messages'][0]['content'] else 'ru'
  content=body['messages'][-1]['content']
  try:
   data=json.loads(content); result=json.dumps({str(line['n']): ('Bonjour ' if target=='fr' else 'Привет ')+str(line['n']) for line in data['lines']},ensure_ascii=False)
  except (ValueError,KeyError,TypeError): result='Привет'
  response=json.dumps({'choices':[{'message':{'content':result}}]},ensure_ascii=False).encode()
  self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(response)));self.end_headers();self.wfile.write(response)
server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler)
threading.Thread(target=server.serve_forever,daemon=True).start()
def check(value,label):
 global checks
 if not value: raise AssertionError(label)
 checks+=1; print('PASS '+label,flush=True)
def run(*args,code=0):
 p=subprocess.run([str(exe),*map(str,args)],capture_output=True,encoding='utf-8',errors='replace',timeout=90)
 if p.returncode!=code: print(p.stdout,p.stderr,flush=True)
 check(p.returncode==code,'CLI exit '+str(code)+' '+str(args[1] if len(args)>1 else args[0]))
 if p.returncode!=code: print(p.stdout,p.stderr)
 return p
try:
 p=run('--help-json');check('ocr-prompt' in json.loads(p.stdout)['options'],'machine-readable option inventory')
 out=root/'text';p=run(source,'ass','--output-folder:'+str(out),'--overwrite','--json');check(json.loads(p.stdout)['failures']==0,'JSON successful conversion');check((out/'sample.ass').read_text(encoding='utf-8').count('Dialogue:')==2,'ASS real output preserves both cues')
 before=(out/'sample.ass').read_bytes();run(source,'ass','--output-folder:'+str(out),'--json',code=1);check((out/'sample.ass').read_bytes()==before,'overwrite guard preserves existing file')
 p=run(source,'srt','--output-filename:'+str(source),'--overwrite','--json',code=1);check(source.read_text(encoding='utf-8').startswith('1\n'),'source overwrite rejected')
 for fmt in ['imsc-image','bdn-image','bdn-8bit','dvd-sup']:
  output=root/fmt
  run(source,fmt,'--output-filename:'+str(output),'--resolution:720x576','--json')
  check(output.exists(),'real '+fmt+' output exists')
  run(source,fmt,'--output-filename:'+str(output),'--resolution:720x576','--json','--overwrite')
  check(output.exists(),'image export replacement succeeds for '+fmt)
 target=root/'translation';p=run(source,'srt','--output-folder:'+str(target),'--translate:ollama','--translate-to:ru,fr','--api-url:http://127.0.0.1:'+str(server.server_port)+'/v1/chat/completions','--model:test','--json','--overwrite')
 check(len(json.loads(p.stdout)['results'])==2,'two translation targets return separate results')
 check((target/'sample.ru.srt').exists() and (target/'sample.fr.srt').exists(),'two target files saved')
 check(len(requests)==2 and all(len(json.loads(r['messages'][-1]['content'])['lines'])==2 for r in requests),'advanced CLI sends real cue batches')
 check('Bonjour' in (target/'sample.fr.srt').read_text(encoding='utf-8'),'target translations stay separate')
 run(source,'srt','--output-filename:'+str(root/'collision.srt'),'--translate:compatible','--translate-to:ru,fr','--json',code=1);check(not (root/'collision.srt').exists(),'multi-target output collision rejected before requests')
 run(source,'imsc-image','--output-filename:'+str(root/'invalid-images'),'--override-position:10','--json',code=1);check(not (root/'invalid-images').exists(),'invalid image position leaves no partial output')
 print('PASS TOTAL '+str(checks))
finally: server.shutdown()
